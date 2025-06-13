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
using Confluent.Kafka;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using System.Text;

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
            SendPayloadCommand = new RelayCommand(async () => await SendPayload());
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
                aes.KeySize = 256;
                aes.GenerateKey();

                byte[] aesKey = aes.Key;

                // Encrypt AES key with the public key
                using RSA rsa = RSA.Create();

                // Import the public key directly using RSA class
                rsa.ImportFromPem(publicKey.ToCharArray());

                byte[] encryptedAesKey = rsa.Encrypt(aesKey, RSAEncryptionPadding.Pkcs1);

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
            using (GZipStream gzip = new GZipStream(ms, CompressionMode.Compress, true))
            {
                using StreamWriter writer = new StreamWriter(gzip, new UTF8Encoding(false));
                writer.Write(content);
            }

            return ms.ToArray();
        }


        //private byte[] EncryptWithAES(byte[] data, byte[] key)
        //{
        //    using Aes aes = Aes.Create();
        //    aes.Key = key;
        //    aes.GenerateIV();

        //    using MemoryStream ms = new MemoryStream();
        //    using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
        //    {
        //        cs.Write(data, 0, data.Length);
        //        cs.FlushFinalBlock();
        //    }

        //    return aes.IV.Concat(ms.ToArray()).ToArray();
        //}

        private byte[] EncryptWithAES(byte[] data, byte[] key)
        {
            // AES GCM uses a 12-byte nonce (IV)
            byte[] nonce = new byte[12];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(nonce); // Generate a secure random nonce
            }

            // Define the tag size explicitly (16 bytes for AES GCM)
            const int tagSizeInBytes = 16;

            // Create the AES GCM object with explicit tag size
            using AesGcm aesGcm = new(key, tagSizeInBytes);
            // Encrypted output will be: nonce (12 bytes) + encrypted content + tag (16 bytes)
            byte[] encryptedContent = new byte[data.Length];
            byte[] tag = new byte[tagSizeInBytes]; // Use the explicit tag size

            // Encrypt the data
            aesGcm.Encrypt(nonce, data, encryptedContent, tag);

            // Combine nonce, encrypted content, and tag into a single byte array
            return [.. nonce, .. encryptedContent, .. tag];
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

        private async void GeneratePayload()
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

            if (SelectedReceiver == null)
            {
                MessageBox.Show("Please select a receiver first from step 1", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Fetch the public key
            var publicKeyInfo = await PublicKeyStoreService.FetchPublicKey(
                environment.ServicesApiUrl,
                SelectedReceiver.Idp.FirstOrDefault(),
                certificate);


            // Initialize the model with the values directly
            var offerNLPIEvent = new OfferNLPIEventType
            {
                Id = Guid.NewGuid().ToString(),
                Source = "http://www.myecohub.ch/",
                Specversion = "1.0",
                Type = "data",
                DataContentType = "application/json",
                DataSchema = "http://www.myecohub.ch/ib2b/offer/nlpi/v0.2.0",
                Subject = "Test subject",
                Time = DateTime.UtcNow.ToString("yyyy-MM-ddThh:mm:ss.fffZ"),
                Data = new Data
                {
                    Payload = EncryptedContent,
                    Links = new List<Links>(), // Empty list, as per the provided JSON
                    EncryptionKey = EncryptedAESKey,
                    PublicKeyVersion = publicKeyInfo.version,
                },
                LicenceKey = profile.LicenseKey,
                UserAgent = new UserAgent
                {
                    Name = "SAF testing tool",
                    Version = "1.0"
                },
                EventReceiver = new EventReceiver
                {
                    Category = "broker",
                    Id = SelectedReceiver.Idp.FirstOrDefault(),
                },
                EventSender = new EventSender
                {
                    Category = "insurer",
                    Id = profile.IdpNumber.ToString(),
                },
                ProcessId = Guid.NewGuid().ToString(),
                ProcessStatus = "active",
                SubProcessName = "request",
                ProcessName = "offer.nlpi",
                SubProcessStatus = "Created"
            };


            // Serialize the object and format it with indentation
            string jsonString = JsonConvert.SerializeObject(offerNLPIEvent, Formatting.Indented);


            PayloadContent = jsonString;
        }

        public ICommand SendPayloadCommand { get; }

        public async Task SendPayload()
        {
            var profile = _dbContext.Profiles.FirstOrDefault();
            if (profile == null)
            {
                MessageBox.Show("Not connected. Please configure general settings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Retrieve environment settings from the database
            var env = _dbContext.EnvironmentSettings.FirstOrDefault(e => e.EnvironmentName == profile.SelectedEnvironment);

            if (env == null)
            {
                MessageBox.Show("Environment settings not found!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Read SSL certificate information from the environment and file system
            var certificate = GetClientCertificate();  // Implement your logic to get client certificate
            var publicKeyPem = certificate.ExportCertificatePem();
            var privateKey = certificate.GetRSAPrivateKey();
            var privateKeyPem = privateKey.ExportRSAPrivateKeyPem();
            //var caPem = File.ReadAllText("ca.pem");  // Update this with your correct path to the CA file if required

            // Kafka configuration details fetched from the DB
            string bootstrapServers = $"{env.CsmHost}:9092";  // Example, use the database entry for CsmHost
            string topic = "eh.saf.in";  // The topic you want to send the message to

            // Kafka producer configuration
            var config = new ProducerConfig
            {
                BootstrapServers = bootstrapServers,
                SecurityProtocol = SecurityProtocol.Ssl,
                SslCertificatePem = publicKeyPem,
                SslKeyPem = privateKeyPem,
                //SslCaPem = caPem, // Uncomment if you need to use the CA PEM
            };

            // Schema registry configuration (keep this unchanged or use DB if necessary)
            var schemaRegistryConfig = new SchemaRegistryConfig
            {
                Url = "https://psrc-qrk9d.westeurope.azure.confluent.cloud:443",
                BasicAuthUserInfo = "FCYTB2BG73BWKLZ5:juvZLo3Frvgoqn9Mb5dDJjaXx4NAYf1PwY+k5egoUBEHIYYCnmgzJE/M7uCCYjPv"
            };


            try
            {
                // Parse the JSON input
                var myEvent = Newtonsoft.Json.JsonConvert.DeserializeObject<OfferNLPIEventType>(PayloadContent);

                var schemaRegistry = new CachedSchemaRegistryClient(schemaRegistryConfig);


                // Create the Kafka producer
                using var producer = new ProducerBuilder<ProcessIdType, OfferNLPIEventType>(config)
                    .SetValueSerializer(new JsonSerializer<OfferNLPIEventType>(schemaRegistry, new JsonSerializerConfig
                    {
                        BufferBytes = 100,
                        UseLatestVersion = true,
                        AutoRegisterSchemas = false,
                        SubjectNameStrategy = SubjectNameStrategy.Topic
                    }))
                    .SetKeySerializer(new JsonSerializer<ProcessIdType>(schemaRegistry, new JsonSerializerConfig
                    {
                        UseLatestVersion = true,
                        AutoRegisterSchemas = false,
                        //SubjectNameStrategy = SubjectNameStrategy.Topic
                    }))
                    .Build();

                // Create a message to send
                var message = new Message<ProcessIdType, OfferNLPIEventType>
                {
                    Key = new ProcessIdType { ProcessId = Guid.Parse(myEvent.ProcessId) },
                    Value = myEvent
                };

                // Send the message to the Kafka topic
                var deliveryReport = await producer.ProduceAsync(topic, message);

                MessageBox.Show($"Message sent to topic {topic}. Offset: {deliveryReport.Offset}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                MessageBox.Show($"Invalid JSON format: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (ProduceException<ProcessIdType, OfferNLPIEventType> ex)
            {
                MessageBox.Show($"Kafka error: {ex.Error.Reason}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}
