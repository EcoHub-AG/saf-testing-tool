using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StandardApiFrameworkTool.Exceptions;
using StandardApiFrameworkTool.Helpers;
using StandardApiFrameworkTool.Models;
using StandardApiFrameworkTool.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace StandardApiFrameworkTool.ViewModels
{
    public class ReceiveDataViewModel : ViewModelBase
    {
        private readonly AppDbContext _dbContext;

        public ReceiveDataViewModel()
        {
            ThreadList = new ObservableCollection<ThreadItem>();
            PrivateKeys = new ObservableCollection<PrivateKey>();
            GenerateKeyPairCommand = new RelayCommand(GenerateKeyPair);
            ActivateKeyCommand = new RelayCommand(async () => await ActivateKey());

            GenerateSignatureKeyPairCommand = new RelayCommand(GenerateSignatureKeyPair);
            ActivateSignatureKeyCommand = new RelayCommand(async () => await ActivateSignatureKey());

            _dbContext = new AppDbContext();

            PrivateKeys = new ObservableCollection<PrivateKey>(_dbContext.PrivateKeys.Select(pk => new PrivateKey
            {
                Id = pk.Id,
                Version = pk.Version,
                CreatedAt = pk.CreatedAt,
                IsActive = pk.IsActive,
            }));

            SignatureKeys = new ObservableCollection<SignatureKey>(_dbContext.SignatureKeys.Select(pk => new SignatureKey
            {
                Id = pk.Id,
                Version = pk.Version,
                CreatedAt = pk.CreatedAt,
                IsActive = pk.IsActive,
            }));

            StartConsumer();
        }

        #region Receive event
        private ObservableCollection<ThreadItem> _threadList;
        public ObservableCollection<ThreadItem> ThreadList
        {
            get => _threadList;
            set
            {
                _threadList = value;
                OnPropertyChanged(nameof(ThreadList));
            }
        }

        private ThreadItem _selectedThread;
        public ThreadItem SelectedThread
        {
            get => _selectedThread;
            set
            {
                _selectedThread = value;
                if (!string.IsNullOrEmpty(value?.Payload))
                {
                    SelectedThreadPayload = DecryptAndUnZip(value.Payload);
                    SelectedThreadPayload = FormatJson(SelectedThreadPayload);
                }
                OnPropertyChanged(nameof(SelectedThread));
                
            }
        }

        

        private string _selectedThreadPayload;
        public string SelectedThreadPayload
        {
            get => _selectedThreadPayload;
            set
            {
                _selectedThreadPayload = value;
                OnPropertyChanged(nameof(SelectedThreadPayload));
            }
        }

        private async void StartConsumer()
        {
            await Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        // Fetch profile and environment from DB
                        var profile = _dbContext.Profiles.FirstOrDefault();
                        var env = profile != null
                            ? _dbContext.EnvironmentSettings.FirstOrDefault(e => e.EnvironmentName == profile.SelectedEnvironment)
                            : null;

                        if (profile != null && env != null)
                        {
                            // If both are available, start the consumer
                            StartKafkaConsumer();
                            break; // Exit the loop after the consumer starts
                        }

                        // Retry after a delay if not available
                        await Task.Delay(TimeSpan.FromSeconds(10));
                    }
                }
                catch (Exception ex)
                {
                    // Log any top-level exception
                    MessageBox.Show($"Error in Kafka consumer: {ex.Message}");
                }
            });
        }


        private async void StartKafkaConsumer()
        {
            var profile = _dbContext.Profiles.FirstOrDefault();
            var env = _dbContext.EnvironmentSettings.FirstOrDefault(e => e.EnvironmentName == profile.SelectedEnvironment);

            await Task.Run(async () =>
            {
                try
                {
                    var certificate = GetClientCertificate();
                    var publicKeyPem = certificate.ExportCertificatePem();
                    var privateKey = certificate.GetRSAPrivateKey();
                    var privateKeyPem = privateKey.ExportRSAPrivateKeyPem();
                    // var caPem = File.ReadAllText("ca.pem");

                    string bootstrapServers = $"{env.CsmHost}:9092"; // Replace with your Kafka broker(s) address
                    string topic = profile.OutTopic;

                    var config = new ConsumerConfig
                    {
                        BootstrapServers = bootstrapServers,
                        AutoOffsetReset = AutoOffsetReset.Earliest,
                        SecurityProtocol = SecurityProtocol.Ssl,
                        SslCertificatePem = publicKeyPem,
                        SslKeyPem = privateKeyPem,
                        //SslCaPem = caPem,
                        EnableAutoCommit = false,
                        GroupId = $"CG-00001-{profile.IdpNumber}",
                    };

                    using (var consumer = new ConsumerBuilder<Ignore, string>(config).Build())
                    {
                        consumer.Subscribe(topic);

                        CancellationTokenSource cts = new CancellationTokenSource();

                        Console.CancelKeyPress += (_, e) =>
                        {
                            e.Cancel = true; // Prevent the process from exiting immediately
                            cts.Cancel();
                        };

                        try
                        {
                            //IsLoading = true;
                            while (!cts.Token.IsCancellationRequested)
                            {
                                try
                                {
                                    var consumeResult = consumer.Consume(cts.Token);

                                    // Use Dispatcher to update the UI safely
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        ThreadList.Add(new ThreadItem
                                        {
                                            Payload = consumeResult.Value,
                                            Timestamp = DateTime.Now.ToString(),
                                            Title = ExtractData(consumeResult.Value),
                                        });
                                    });
                                }
                                catch (ConsumeException ex)
                                {
                                    MessageBox.Show($"Kafka consume error: {ex.Error.Reason}");
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"Error during message consumption: {ex.Message}");
                                }
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            // Log when consumer is cancelled
                            MessageBox.Show("Consumer loop has been canceled.");
                        }
                        finally
                        {
                            consumer.Close();
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log any top-level exception
                    MessageBox.Show($"Error in Kafka consumer: {ex.Message}");
                }
            });

        }

        public string ExtractData(string jsonString)
        {
            try
            {
                // Parse the JSON string into a JToken object
                JToken json = JToken.Parse(jsonString);

                // Extract the "Id" field and get the first part of the GUID
                string fullGuid = json["id"]?.ToString();
                string firstPartOfGuid = fullGuid?.Split('-')[0];

                // Extract the "EventSender.Id" field
                string senderId = json["eventSender"]?["id"]?.ToString();

                // Output the extracted values
                return $"{senderId} - {firstPartOfGuid}";
            }
            catch (Exception ex)
            {
                return "<Unknown>";
            }
        }

        private string FormatJson(string json)
        {
            try
            {

                string pattern = @"\{.*\}";

                // Use regex to extract the JSON part
                Match match = Regex.Match(json, pattern);

                if (match.Success)
                {
                    json = match.Value;
                }

                // Load and parse the JSON
                var parsedJson = JToken.Parse(json);
                

                // Format and indent the JSON for readability
                var formattedJson = parsedJson.ToString(Newtonsoft.Json.Formatting.Indented);


                return formattedJson;
            }
            catch (Exception ex)
            {
                return $"Error formatting JSON: {ex.Message}";
            }
        }

        private string DecryptAndUnZip(string content)
        {
            CommonEventType offerNlpi;
            try
            {
                string pattern = @"\{.*\}";

                // Use regex to extract the JSON part
                Match match = Regex.Match(content, pattern);

                if (match.Success)
                {
                    string json = match.Value;
                    offerNlpi = JsonConvert.DeserializeObject<CommonEventType>(json);
                }
                else
                {
                    throw new Exception("invalid JSON");
                }
            }
            catch (Exception)
            {
                return "Event Deserialize failed";
            }

            if (offerNlpi.Data?.Payload != null)
            {
                try
                {
                    // 1. Retrieve and decrypt the AES key using the private RSA key
                    string encryptedAesKeyBase64 = offerNlpi.Data.EncryptionKey; // The encrypted AES key
                    byte[] encryptedAesKey = Convert.FromBase64String(encryptedAesKeyBase64);

                    byte[] aesKey = DecryptAESKeyWithPrivateKey(encryptedAesKey, offerNlpi);

                    if (aesKey == null)
                    {
                        MessageBox.Show("Failed to decrypt AES key.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return "Failed to decrypt AES key.";
                    }

                    // 2. Retrieve the encrypted content (Base64 encoded)
                    string encryptedBase64Content = offerNlpi.Data.Payload; // The encrypted content
                    byte[] encryptedContent = Convert.FromBase64String(encryptedBase64Content);

                    // 3. Decrypt the content with the decrypted AES key
                    byte[] decryptedContent = DecryptWithAES(encryptedContent, aesKey);

                    // 4. Unzip the decrypted content
                    string unzippedContent = UnzipContent(decryptedContent);

                    // 5. Display the unzipped content in the content editor

                    offerNlpi.Data.Payload = unzippedContent;

                    return JsonConvert.SerializeObject(offerNlpi);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return ex.ToString();
                }
            }

            return content;
        }

        // Method to decrypt the AES key using the private RSA key
        private byte[] DecryptAESKeyWithPrivateKey(byte[] encryptedAesKey, CommonEventType offerNLPI)
        {
            try
            {
                // Retrieve the private key (replace with your actual RSA private key decryption logic)
                var privateKeyInfo = _dbContext.PrivateKeys.FirstOrDefault(p => p.Version == offerNLPI.Data.PublicKeyVersion);
                if (privateKeyInfo == null)
                {
                    MessageBox.Show($"No private key found in the database for version {offerNLPI.Data.PublicKeyVersion}.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                // Set up RSA for decryption
                RSA rsaPrivate = RSA.Create();

                // Import the private key
                rsaPrivate.ImportFromPem(privateKeyInfo.Key);

                // Decrypt the AES key
                byte[] decryptedAesKey = rsaPrivate.Decrypt(encryptedAesKey, RSAEncryptionPadding.OaepSHA256);

                return decryptedAesKey;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error decrypting AES key: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        // Method to decrypt the content using AES
        private byte[] DecryptWithAES_CBC(byte[] encryptedData, byte[] key)
        {
            using (Aes aes = Aes.Create())
            {
                // The first 16 bytes are the AES IV, the rest is the encrypted content
                byte[] iv = encryptedData.Take(16).ToArray();
                byte[] encryptedContent = encryptedData.Skip(16).ToArray();

                aes.Key = key;
                aes.IV = iv;

                using (ICryptoTransform decryptor = aes.CreateDecryptor())
                using (MemoryStream ms = new MemoryStream())
                using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Write))
                {
                    cs.Write(encryptedContent, 0, encryptedContent.Length);
                    cs.FlushFinalBlock();
                    return ms.ToArray();
                }
            }
        }

        private byte[] DecryptWithAES(byte[] encryptedData, byte[] key)
        {
            // Define the tag size explicitly (16 bytes for AES GCM)
            const int tagSizeInBytes = 16;

            // The first 12 bytes are the AES GCM nonce (IV), the last 16 bytes are the authentication tag, and the rest is the encrypted content
            byte[] nonce = encryptedData.Take(12).ToArray();
            byte[] tag = encryptedData.Skip(encryptedData.Length - tagSizeInBytes).ToArray();
            byte[] encryptedContent = encryptedData.Skip(12).Take(encryptedData.Length - 12 - tagSizeInBytes).ToArray();

            // Create the AES GCM object with explicit tag size
            using AesGcm aesGcm = new AesGcm(key, tagSizeInBytes);
            byte[] decryptedData = new byte[encryptedContent.Length];

            // Decrypt the data
            aesGcm.Decrypt(nonce, encryptedContent, tag, decryptedData);

            return decryptedData;
        }

        // Method to unzip the decrypted content
        private string UnzipContent(byte[] compressedData)
        {
            using (var inputStream = new MemoryStream(compressedData))
            using (var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress))
            using (var outputStream = new MemoryStream())
            {
                gzipStream.CopyTo(outputStream);
                return Encoding.UTF8.GetString(outputStream.ToArray());
            }
        }

        #endregion

        #region Encryption
        private ObservableCollection<PrivateKey> _privateKeys;

        public ObservableCollection<PrivateKey> PrivateKeys
        {
            get { return _privateKeys; }
            set 
            { 
                _privateKeys = value;
                OnPropertyChanged(nameof(PrivateKeys));
            }
        }

        private string _generatedPublicKey;

        public string GeneratedPublicKey
        {
            get { return _generatedPublicKey; }
            set 
            { 
                _generatedPublicKey = value;
                OnPropertyChanged(nameof(GeneratedPublicKey));
            }
        }

        private string _generatedPrivateKey;

        public string GeneratedPrivateKey
        {
            get { return _generatedPrivateKey; }
            set 
            { 
                _generatedPrivateKey = value;
                OnPropertyChanged(nameof(GeneratedPrivateKey));
            }
        }

        public ICommand GenerateKeyPairCommand { get; }
        public ICommand ActivateKeyCommand { get; }

        private void GenerateKeyPair()
        {

            (GeneratedPublicKey, GeneratedPrivateKey) = RSAKeyHelper.GenerateKeyPair();
            OnPropertyChanged(nameof(GeneratedPublicKey));
            OnPropertyChanged(nameof(GeneratedPrivateKey));
        }

        private async Task ActivateKey()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ((MainViewModel)Application.Current.MainWindow.DataContext).IsProcessing = true;
            });

            await UploadAndSaveToDatabase();
            GeneratedPublicKey = string.Empty;
            GeneratedPrivateKey = string.Empty;

            Application.Current.Dispatcher.Invoke(() =>
            {
                ((MainViewModel)Application.Current.MainWindow.DataContext).IsProcessing = false;
            });
        }

        #region properties signature keys

        public ICommand GenerateSignatureKeyPairCommand { get; }
        public ICommand ActivateSignatureKeyCommand { get; }

        private ObservableCollection<SignatureKey> _signatureKeys;

        public ObservableCollection<SignatureKey> SignatureKeys
        {
            get { return _signatureKeys; }
            set { _signatureKeys = value; OnPropertyChanged(nameof(SignatureKeys)); }
        }

        private string _generatedSignaturePublicKey;

        public string GeneratedSignaturePublicKey
        {
            get { return _generatedSignaturePublicKey; }
            set
            {
                _generatedSignaturePublicKey = value;
                OnPropertyChanged(nameof(GeneratedSignaturePublicKey));
            }
        }

        private string _generatedSignaturePrivateKey;

        public string GeneratedSignaturePrivateKey
        {
            get { return _generatedSignaturePrivateKey; }
            set
            {
                _generatedSignaturePrivateKey = value;
                OnPropertyChanged(nameof(GeneratedSignaturePrivateKey));
            }
        }

        private void GenerateSignatureKeyPair()
        {

            (GeneratedSignaturePublicKey, GeneratedSignaturePrivateKey) = ECDSAKeyHelper.GenerateECDsaKeyPair();
            OnPropertyChanged(nameof(GeneratedSignaturePublicKey));
            OnPropertyChanged(nameof(GeneratedSignaturePrivateKey));
        }

        private async Task ActivateSignatureKey()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ((MainViewModel)Application.Current.MainWindow.DataContext).IsProcessing = true;
            });

            await UploadSignatureKeyAndSaveToDatabase();
            GeneratedSignaturePublicKey = string.Empty;
            GeneratedSignaturePrivateKey = string.Empty;

            Application.Current.Dispatcher.Invoke(() =>
            {
                ((MainViewModel)Application.Current.MainWindow.DataContext).IsProcessing = false;
            });
        }

        #endregion

        private async Task UploadSignatureKeyAndSaveToDatabase()
        {
            if (string.IsNullOrWhiteSpace(GeneratedSignaturePublicKey) 
                || string.IsNullOrWhiteSpace(GeneratedSignaturePrivateKey))
            {
                MessageBox.Show("Empty public key or private key. Please generate a key pair first.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // validate 
            var isValid = ECDSAKeyHelper.ValidateECDsaKeyPair(GeneratedSignaturePublicKey, GeneratedSignaturePrivateKey);

            if (!isValid)
            {
                MessageBox.Show("Validation failed.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var lastVersion = "1.0.0";
            try
            {
                lastVersion = _dbContext.SignatureKeys.Max(x => x.Version);
            }
            catch (Exception ex)
            {

            }
                
            var currentVersion = IncreaseVersion(lastVersion);


            int maxTries = 15;

            for (int i = 0; i < maxTries; i++)
            {
                try
                {
                    var success = await UploadSignaturePublicKey(currentVersion);
                    if (!success)
                    {
                        return;
                    }
                    break;
                }
                catch (KeyVersionExists kve)
                {
                    currentVersion = IncreaseVersion(currentVersion);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            _dbContext.SignatureKeys.ToList().ForEach(x => x.IsActive = false);

            _dbContext.SignatureKeys.Add(new SignatureKey
            {
                Version = currentVersion,
                Key = GeneratedSignaturePrivateKey,
                IsActive = true,
                CreatedAt = DateTime.Now,
            });

            _dbContext.SaveChanges();
            SignatureKeys = new ObservableCollection<SignatureKey>(_dbContext.SignatureKeys.Select(pk => new SignatureKey
            {
                Id = pk.Id,
                Version = pk.Version,
                CreatedAt = pk.CreatedAt,
                IsActive = pk.IsActive,
            }));

            // Call service to upload and activate the key (implement service logic)
            MessageBox.Show("Public key activated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task UploadAndSaveToDatabase()
        {
            if (string.IsNullOrWhiteSpace(GeneratedPublicKey) || string.IsNullOrWhiteSpace(GeneratedPrivateKey))
            {
                MessageBox.Show("Empty public key or private key. Please generate a key pair first.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // validate 
            var isValid = RSAKeyHelper.ValidateKeyPair(GeneratedPublicKey, GeneratedPrivateKey);

            if (!isValid)
            {
                MessageBox.Show("Validation failed.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var lastVersion = "1.0.0";
            try
            {
                lastVersion = _dbContext.PrivateKeys.Max(x => x.Version);
            }
            catch (Exception ex)
            {

            }
            var currentVersion = IncreaseVersion(lastVersion);


            int maxTries = 15;

            for (int i = 0; i < maxTries; i++)
            {
                try
                {
                    var success = await UploadPublicKey(currentVersion);
                    if (!success)
                    {
                        return;
                    }
                    break;
                }
                catch(KeyVersionExists kve)
                {
                    currentVersion = IncreaseVersion(currentVersion);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            _dbContext.PrivateKeys.ToList().ForEach(x => x.IsActive = false);

            _dbContext.PrivateKeys.Add(new PrivateKey
            {
                Version = currentVersion,
                Key = GeneratedPrivateKey,
                IsActive = true,
                CreatedAt = DateTime.Now,
            });

            _dbContext.SaveChanges();
            PrivateKeys = new ObservableCollection<PrivateKey>(_dbContext.PrivateKeys.Select(pk => new PrivateKey
            {
                Id = pk.Id,
                Version = pk.Version,
                CreatedAt = pk.CreatedAt,
                IsActive = pk.IsActive,
            }));

            // Call service to upload and activate the key (implement service logic)
            MessageBox.Show("Public key activated successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private string IncreaseVersion(string? lastVersion)
        {
            // If the input is null or empty, return the default version
            if (string.IsNullOrEmpty(lastVersion))
            {
                return "1.0.0";
            }

            try
            {
                // Split the version string into parts
                string[] parts = lastVersion.Split('.');

                // Parse each part as an integer
                int major = int.Parse(parts[0]);

                // Increment the major version, reset minor and patch
                major++;

                // Return the new version string
                return $"{major}.0.0";
            }
            catch (FormatException)
            {
                // Handle cases where parsing fails
                throw new ArgumentException($"Invalid version format: {lastVersion}");
            }
            catch (IndexOutOfRangeException)
            {
                // Handle cases where the version string is malformed
                throw new ArgumentException($"Invalid version format: {lastVersion}");
            }
        }

        private async Task<bool> UploadSignaturePublicKey(string version)
        {
            var profile = _dbContext.Profiles.FirstOrDefault();
            if (profile == null)
            {
                MessageBox.Show("Profile information is missing. Please check your general settings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            var environment = _dbContext.EnvironmentSettings.FirstOrDefault(e => e.EnvironmentName == profile.SelectedEnvironment);
            if (environment == null)
            {
                MessageBox.Show("Environment not found. Please check your general settings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }


            var certificate = GetClientCertificate();
            string baseAddress = environment.ServicesApiUrl;

            var keyId = await PublicKeyStoreService.UploadPublicKey(baseAddress, GeneratedSignaturePublicKey, version, 365, "signature", certificate);

            await PublicKeyStoreService.ActivatePublicKey(baseAddress, keyId.ToString(), certificate);
            return true;
        }

        private async Task<bool> UploadPublicKey(string version)
        {
            var profile = _dbContext.Profiles.FirstOrDefault();
            if (profile == null)
            {
                MessageBox.Show("Profile information is missing. Please check your general settings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            var environment = _dbContext.EnvironmentSettings.FirstOrDefault(e => e.EnvironmentName == profile.SelectedEnvironment);
            if (environment == null)
            {
                MessageBox.Show("Environment not found. Please check your general settings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            

            var certificate = GetClientCertificate();
            string baseAddress = environment.ServicesApiUrl;
            
            var keyId = await PublicKeyStoreService.UploadPublicKey(baseAddress, GeneratedPublicKey, version, 365, "encryption", certificate);

            await PublicKeyStoreService.ActivatePublicKey(baseAddress, keyId.ToString() , certificate);
            return true;
        }

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


        #endregion
    }
}
