using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using StandardApiFrameworkTool.Models;
using StandardApiFrameworkTool.Services;
using StandardApiFrameworkTool.Helpers;
using System.IO.Compression;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Security.Cryptography.Pkcs;
using Newtonsoft.Json;

namespace StandardApiFrameworkTool.ViewModels
{
    public class SendDataViewModel : ViewModelBase
    {
        private readonly AppDbContext _dbContext;

        public SendDataViewModel()
        {
            _dbContext = new AppDbContext();
            Receivers = new ObservableCollection<SAFReceiverResponse>();

            LoadReceiversCommand = new RelayCommand(async () => await LoadReceiversAsync());
            EncryptContentCommand = new RelayCommand(async () => await FetchAndEncryptContentAsync());
            GeneratePayloadCommand = new RelayCommand(GeneratePayload);

        }

        

        #region step 1 - select membership
        private ObservableCollection<SAFReceiverResponse> _receivers;
        private SAFReceiverResponse _selectedReceiver;

        public ObservableCollection<SAFReceiverResponse> Receivers
        {
            get => _receivers;
            set
            {
                _receivers = value;
                OnPropertyChanged(nameof(Receivers));
            }
        }

        public SAFReceiverResponse SelectedReceiver
        {
            get => _selectedReceiver;
            set
            {
                _selectedReceiver = value;
                OnPropertyChanged(nameof(SelectedReceiver));
            }
        }

        public ICommand LoadReceiversCommand { get; }


        // methods


        private X509Certificate2 GetClientCertificate()
        {
            var profile = _dbContext.Profiles.FirstOrDefault();
            var techUser = _dbContext.TechUsers.FirstOrDefault();

            if (techUser == null || profile == null)
            {
                throw new InvalidOperationException("Not connected. Please configure general settings.");
            }

            // Decode the Base64 string to get the byte array
            byte[] certificateBytes = Convert.FromBase64String(techUser.TechUserCert);

            // Create an X509Certificate2 object from the byte array
            X509Certificate2 certificate = new X509Certificate2(
                certificateBytes,
                profile.Password,
                X509KeyStorageFlags.Exportable);

            return certificate;
        }

        private async Task LoadReceiversAsync()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ((MainViewModel)Application.Current.MainWindow.DataContext).IsProcessing = true;
            });

            try
            {
                var profile = _dbContext.Profiles.FirstOrDefault();
                if (profile == null)
                {
                    MessageBox.Show("Not connected. Please configure general settings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var certificate = GetClientCertificate();

                var environment = _dbContext.EnvironmentSettings.FirstOrDefault(e => e.EnvironmentName == profile.SelectedEnvironment);
                if (environment == null)
                {
                    MessageBox.Show("Environment information is missing.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string baseAddress = environment.ServicesApiUrl;

                var response = await TechUserService.GetReceiver(profile.LicenseKey, profile.Password, baseAddress, certificate);

                if (response != null && response.Count > 0)
                {
                    Receivers.Clear();
                    foreach (var company in response)
                    {
                        Receivers.Add(company);
                    }

                    SelectedReceiver = Receivers.FirstOrDefault();
                }
                else
                {
                    MessageBox.Show("No receivers found.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while loading receivers: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                ((MainViewModel)Application.Current.MainWindow.DataContext).IsProcessing = false;
            });
        }

        #endregion

        #region step 2 - zip and encrypt
        private string _inputContent;
        private string _encryptedAESKey;
        private string _encryptedContent;

        public string InputContent
        {
            get => _inputContent;
            set
            {
                _inputContent = value;
                OnPropertyChanged(nameof(InputContent));
            }
        }

        public string EncryptedAESKey
        {
            get => _encryptedAESKey;
            set
            {
                _encryptedAESKey = value;
                OnPropertyChanged(nameof(EncryptedAESKey));
            }
        }

        public string EncryptedContent
        {
            get => _encryptedContent;
            set
            {
                _encryptedContent = value;
                OnPropertyChanged(nameof(EncryptedContent));
            }
        }

        public ICommand EncryptContentCommand { get; }


        // methods

        private async Task FetchAndEncryptContentAsync()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ((MainViewModel)Application.Current.MainWindow.DataContext).IsProcessing = true;
            });
            try
            {
                var profile = _dbContext.Profiles.FirstOrDefault();
                if (profile == null)
                {
                    MessageBox.Show("Profile information is missing. Please check your general settings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var environment = _dbContext.EnvironmentSettings.FirstOrDefault(e => e.EnvironmentName == profile.SelectedEnvironment);
                if (environment == null)
                {
                    MessageBox.Show("Environment not found. Please check your general settings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (string.IsNullOrWhiteSpace(InputContent))
                {
                    MessageBox.Show("Please enter content to encrypt.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var certificate = GetClientCertificate();

                if(SelectedReceiver == null) 
                {
                    MessageBox.Show("Please select a receiver first from step 1", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Fetch the public key
                var publicKeyInfo = await PublicKeyStoreService.FetchPublicKey(
                    environment.ServicesApiUrl, 
                    SelectedReceiver.Idp.FirstOrDefault(), 
                    certificate);
                if (publicKeyInfo == null || string.IsNullOrWhiteSpace(publicKeyInfo.Key))
                {
                    MessageBox.Show("Failed to retrieve a valid public key.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ((MainViewModel)Application.Current.MainWindow.DataContext).IsProcessing = false;
                    });

                    return;
                }

                // Encrypt the content
                EncryptContentWithPublicKey(publicKeyInfo.Key);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                ((MainViewModel)Application.Current.MainWindow.DataContext).IsProcessing = false;
            });
        }

        private void EncryptContentWithPublicKey(string publicKey)
        {
            try
            {
                // 1. Generate AES key
                using Aes aes = Aes.Create();
                aes.KeySize = 128;
                aes.GenerateKey();

                byte[] aesKey = aes.Key;

                // Encrypt AES key with the public key
                RSACryptoServiceProvider rsa = new();

                // Extract and clean the public key
                var publicKeyText = Regex.Replace(publicKey, @"-----BEGIN PUBLIC KEY-----\s*", "");
                publicKeyText = Regex.Replace(publicKeyText, @"\s*-----END PUBLIC KEY-----", "");

                rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyText), out _);
                byte[] encryptedAesKey = rsa.Encrypt(aesKey, true);

                EncryptedAESKey = Convert.ToBase64String(encryptedAesKey);

                // Zip and encrypt the content
                byte[] zippedContent = ZipContent(InputContent);
                byte[] encryptedContent = EncryptWithAES(zippedContent, aesKey);

                EncryptedContent = Convert.ToBase64String(encryptedContent);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error encrypting content: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private byte[] ZipContent(string content)
        {
            using MemoryStream ms = new MemoryStream();
            using (ZipArchive archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
            {
                ZipArchiveEntry entry = archive.CreateEntry("content.txt");
                using StreamWriter writer = new StreamWriter(entry.Open());
                writer.Write(content);
            }
            return ms.ToArray();
        }

        private byte[] EncryptWithAES(byte[] data, byte[] key)
        {
            using Aes aes = Aes.Create();
            aes.Key = key;
            aes.GenerateIV();

            using MemoryStream ms = new MemoryStream();
            using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                cs.Write(data, 0, data.Length);
                cs.FlushFinalBlock();
            }

            return aes.IV.Concat(ms.ToArray()).ToArray();
        }


        #endregion

        #region step 3 - send payload

        private string _payloadContent;

        public string PayloadContent
        {
            get { return _payloadContent; }
            set 
            { 
                _payloadContent = value;
                OnPropertyChanged(nameof(PayloadContent));
            }
        }

        public ICommand GeneratePayloadCommand { get; }

        private void GeneratePayload()
        {
            // Initialize the model with the values directly
            var offerNLPIEvent = new OfferNLPIEventType
            {
                Id = "b7d7cfd3-791e-430d-8791-e883d9c",
                Source = "http://www.myecohub.ch/ck-postman",
                Specversion = "0.3.0",
                Type = "offer.nlpi",
                DataContentType = "application/json",
                DataSchema = "http://www.myecohub.ch/ib2b/offer/nlpi/v0.2.0",
                Subject = "Before Change 1",
                Time = "2023-10-20T13:38:27.792Z",
                Data = new Data
                {
                    Payload = "CVPZIsWfhIPvEPgsSWSPx9utKqeyv4sx4XLS8jFnxdmMQJ6J2i/b/5FPfWmTpKkvLQBTCib1Y+gPO5ilu9jOMbG3eK95vQaxmibY85bCBhdolRqTssjslE97yIgO6bFRdeV1KGZBUxhw1llZccbMtQElrhWmx+D6ioSkpz6WOzMJjHr94DnrdneQ3pDNXXyQiUIfiYv3cuOAzksxB/YY/bCsngwBkTEaNm9MyWfYKRXxBcvgu+b5Pg1INNSMqBUR",
                    Links = new List<Links>(), // Empty list, as per the provided JSON
                    EncryptionKey = "ARaaBp1wIEoMNol15cgce/mMq776//we/RgrhtvZAsq9kP+0xhB0U8fK7H98JeGBubwHexqHsEa84AjCn/D2qHQr/NG4xw99NpKMxhgAjNPe0LBvhd41F25tmNDo54kd6H0SzdOZzssgf6OkK7/kq4wzB6w2s2vGur7gE934T+vH60XHZHUV9cEyO1c9kb7jqo2ROSAVEGkHhcnUZuUv+Y1Sf6D9u63ZXgkL8p7X2e/ybuOfjD7dWPlncL7vyEnBE1CPDsXA8LwLWYn5+A0bjGEps3NqwLIT8jz8ktYkzvGpBV81ak/T6sDHsCn06QCDGHTpytG359tGvdCVPVEkDQ==",
                    PublicKeyVersion = "11.0.0"
                },
                LicenceKey = "M/E49G0HE+rfUHgo1+Tk/yEiDQNzvIsywHvW2w1jyYk=laUAGTNsagFnViq82sq2ltPG82XpQMZZHxEFAiCIzFU=",
                UserAgent = new UserAgent
                {
                    Name = "Clemens Postman",
                    Version = "1.0"
                },
                EventReceiver = new EventReceiver
                {
                    Category = "broker",
                    Id = "IDP5343269"
                },
                EventSender = new EventSender
                {
                    Category = "insurer",
                    Id = "IDP8033870"
                },
                ProcessId = "58991281-3789-4446-94ef-e4d7d005d2e2",
                ProcessStatus = "active",
                SubProcessName = "request",
                ProcessName = "offer.nlpi",
                SubProcessStatus = "Created"
            };

            // Serialize the object and format it with indentation
            string jsonString = JsonConvert.SerializeObject(offerNLPIEvent, Formatting.Indented);


            PayloadContent = jsonString;
        }

        #endregion
    }
}
