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
using System.Security.Cryptography.Xml;
using System.Windows.Media;

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
            UploadEncKeyCommand = new RelayCommand(async () => await ValidateAndUploadEncKey());

            GenerateSignatureKeyPairCommand = new RelayCommand(GenerateSignatureKeyPair);
            UploadSignatureKeyCommand = new RelayCommand(async () => await ValidateAndUploadSignatureKey());

            ActivateSignPublicKeyCommand = new RelayCommand<SignatureKey>(ActivateSignatureKey);
            ActivateEncPublicKeyCommand = new RelayCommand<PrivateKey>(ActivateEncryptionKey);

            _dbContext = new AppDbContext();

            PrivateKeys = new ObservableCollection<PrivateKey>(_dbContext.PrivateKeys.Select(pk => new PrivateKey
            {
                Id = pk.Id,
                Version = pk.Version,
                CreatedAt = pk.CreatedAt,
                Key = pk.Key,
                IsActive = pk.IsActive,
            }));

            SignatureKeys = new ObservableCollection<SignatureKey>(_dbContext.SignatureKeys.Select(pk => new SignatureKey
            {
                Id = pk.Id,
                Version = pk.Version,
                CreatedAt = pk.CreatedAt,
                Key = pk.Key,
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

        private string _topicsInfo;

        public string TopicsInfo
        {
            get { return _topicsInfo; }
            set 
            { 
                _topicsInfo = value; 
                OnPropertyChanged(nameof(TopicsInfo)); 
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

                using (
                    var consumer = new ConsumerBuilder<Ignore, byte[]>(config)
                    .SetPartitionsAssignedHandler((c, partitions) =>
                    {
                        var topics = partitions.Select(p => p.Topic).Distinct();
                        
                        TopicsInfo = "Subscribed Topics: " +
                        string.Join(" • ", topics.Select(t => $"{t}"));
                    })
                    .Build())
                {
                        consumer.Subscribe($"^eh\\.saf\\.{profile.OrgId}(\\..+)?\\.out\\.v1$");

                        var topics = consumer.Assignment
                             .Select(tp => tp.Topic)
                             .Distinct()
                             .ToList();

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
                                    await Application.Current.Dispatcher.Invoke(async () =>
                                    {
                                        (string, Brush) labelTextAndColor = ExtractLabel(consumeResult.Message.Value);
                                        ThreadList.Add(new ThreadItem
                                        {
                                            Payload = GetCleanJson(consumeResult.Message.Value),
                                            Timestamp = DateTime.Now.ToString(),
                                            Title = ExtractData(consumeResult.Message.Value),
                                            Verified = await GetVerifiedString(
                                                GetCleanJson(consumeResult.Message.Value)),
                                            Label = labelTextAndColor.Item1,
                                            LabelColor = labelTextAndColor.Item2,
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

        public static string GetCleanJson(ReadOnlySpan<byte> payload)
        {
            try
            {
                // If this is the Confluent wire format, first byte is 0x00 and next 4 bytes are the schema id.
                int offset = (payload.Length >= 5 && payload[0] == 0x00) ? 5 : 0;

                // From here on, JSON should begin (possibly after whitespace/BOM). Find the first '{' or '['.
                ReadOnlySpan<byte> rest = payload.Slice(offset);
                int jsonStart = FindJsonStart(rest);
                if (jsonStart >= 0) rest = rest.Slice(jsonStart);

                // Decode as UTF-8 JSON (this works for JSON Schema serializer only).
                string jsonText = Encoding.UTF8.GetString(rest);
                return jsonText;
            }
            catch (Exception ex) { return ex.Message; }

        }

        public static string ExtractData(ReadOnlySpan<byte> payload)
        {
            try
            {
                // If this is the Confluent wire format, first byte is 0x00 and next 4 bytes are the schema id.
                int offset = (payload.Length >= 5 && payload[0] == 0x00) ? 5 : 0;

                // From here on, JSON should begin (possibly after whitespace/BOM). Find the first '{' or '['.
                ReadOnlySpan<byte> rest = payload.Slice(offset);
                int jsonStart = FindJsonStart(rest);
                if (jsonStart >= 0) rest = rest.Slice(jsonStart);

                // Decode as UTF-8 JSON (this works for JSON Schema serializer only).
                string jsonText = Encoding.UTF8.GetString(rest);

                // Parse & extract
                var json = JToken.Parse(jsonText);
                string fullGuid = json["id"]?.ToString();
                string firstPartOfGuid = fullGuid?.Split('-')[0];
                string senderId = json["eventSender"]?["id"]?.ToString();

                return $"{senderId} - {firstPartOfGuid}";
            }
            catch
            {
                return "<Unknown>";
            }
        }

        public static (string, Brush) ExtractLabel(ReadOnlySpan<byte> payload)
        {
            try
            {
                // If this is the Confluent wire format, first byte is 0x00 and next 4 bytes are the schema id.
                int offset = (payload.Length >= 5 && payload[0] == 0x00) ? 5 : 0;

                // From here on, JSON should begin (possibly after whitespace/BOM). Find the first '{' or '['.
                ReadOnlySpan<byte> rest = payload.Slice(offset);
                int jsonStart = FindJsonStart(rest);
                if (jsonStart >= 0) rest = rest.Slice(jsonStart);

                // Decode as UTF-8 JSON (this works for JSON Schema serializer only).
                string jsonText = Encoding.UTF8.GetString(rest);

                // Parse & extract
                var json = JToken.Parse(jsonText);
                string processName = json["processName"]?.ToString();

                if (processName == "Invoices")
                {
                    return (processName, Brushes.Purple);
                }
                else if(processName == "Contract")
                {
                    return (processName, Brushes.Orange);
                }
                else if(processName == "Commission")
                {
                    return (processName, Brushes.Green);
                }
                else
                {
                    return (processName, Brushes.Black);
                }
            }
            catch
            {
                return ("Unknown", Brushes.Brown);
            }
        }

        private static int FindJsonStart(ReadOnlySpan<byte> data)
        {
            int i = 0;
            while (i < data.Length)
            {
                // Skip UTF-8 BOM if present
                if (i + 2 < data.Length && data[i] == 0xEF && data[i + 1] == 0xBB && data[i + 2] == 0xBF)
                {
                    i += 3;
                    continue;
                }

                byte b = data[i];
                // Skip whitespace
                if (b == (byte)' ' || b == (byte)'\t' || b == (byte)'\r' || b == (byte)'\n')
                {
                    i++;
                    continue;
                }

                if (b == (byte)'{' || b == (byte)'[') return i;

                // Not whitespace or JSON start—give up (caller will try to parse and fail gracefully)
                return -1;
            }
            return -1;
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

        private async Task<string> GetVerifiedString(string jsonString)
        {
            var payload = string.Empty;
            var payloadSignature = string.Empty;
            var senderIdp = string.Empty;
            var signatureKeyVersion = string.Empty;
            var processName = string.Empty;
            try
            {
                // Parse the JSON string into a JToken object
                JToken json = JToken.Parse(jsonString);

                // Extract the "EventSender.Id" field
                payload = json["data"]?["payload"]?.ToString();
                payloadSignature = json["data"]?["payloadSignature"]?.ToString();
                senderIdp = json["eventSender"]?["id"]?.ToString();
                signatureKeyVersion = json["data"]?["signatureKeyVersion"]?.ToString();
                processName = json["processName"]?.ToString();

            }
            catch (Exception ex)
            {
                return string.Empty;
            }

            var signKey = await GetSignKey(senderIdp, signatureKeyVersion, processName);

            try
            {
                return VerifyPayload(payload, payloadSignature, signKey) ?
                "Verified" :
                string.Empty;
            }
            catch (Exception)
            {

                return string.Empty ;
            }
            
        }

        private async Task<string> GetSignKey(string? senderIdp, string signatureKeyVersion, string processName)
        {
            var profile = _dbContext.Profiles.FirstOrDefault();
            if (profile == null)
            {
                return string.Empty;
            }

            var environment = _dbContext.EnvironmentSettings
                .FirstOrDefault(e => e.EnvironmentName == profile.SelectedEnvironment);
            if (environment == null)
            {
                return string.Empty;
            }

            var certificate = GetClientCertificate();

            // Fetch the public key
            var publicKeyInfo = await PublicKeyStoreService.FetchPublicKey(
                environment.ServicesApiUrl,
                senderIdp,
                certificate);

            if (publicKeyInfo == null || publicKeyInfo.Count == 0)
            {
                return string.Empty;
            }

            var hasEncKey = publicKeyInfo
                .Where(p => p.SupportedProcesses == null || p.SupportedProcesses.Any(x => x.ProcessName == processName))
                .Where(p => p.Version == signatureKeyVersion)
                .Where(p => p.KeyType == "signature")
                .Any();

            if (!hasEncKey)
            {
                return string.Empty;
            }

            var signKey = publicKeyInfo
                .Where(p => p.SupportedProcesses == null || p.SupportedProcesses.Any(x => x.ProcessName == processName))
                .Where(p => p.Version == signatureKeyVersion)
                .Where(p => p.KeyType == "signature")
                .FirstOrDefault();

            return signKey.Key;
        }

        bool VerifyPayload(string payload, string base64Signature, string publicKeyPem)
        {
            
            // 1) Load the public key
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(publicKeyPem.AsSpan());

            // 2) Convert inputs
            byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
            byte[] signatureBytes = Convert.FromBase64String(base64Signature);

            // 3) Verify (hash inside VerifyData)
            return ecdsa.VerifyData(
                payloadBytes,
                signatureBytes,
                HashAlgorithmName.SHA384,
                DSASignatureFormat.Rfc3279DerSequence // must match the signer
            );
        }

        private string DecryptAndUnZip(string content)
        {
            CommonEventType offerNlpi;
            try
            {
                string pattern = @"\{.*\}";

                // Use regex to extract the JSON part
                Match match = Regex.Match(content, pattern, RegexOptions.Singleline);

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

        #region Encryption Key
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
        public ICommand UploadEncKeyCommand { get; }

        private void GenerateKeyPair()
        {

            (GeneratedPublicKey, GeneratedPrivateKey) = RSAKeyHelper.GenerateKeyPair();
            OnPropertyChanged(nameof(GeneratedPublicKey));
            OnPropertyChanged(nameof(GeneratedPrivateKey));
        }

        private async Task ValidateAndUploadEncKey()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ((MainViewModel)Application.Current.MainWindow.DataContext).IsProcessing = true;
            });

            (var cert, var baseAddress) = GetCertificateAndBaseAddress();

            if (cert == null) { return; }
            
            // Fetch the public key
            var publicKeyInfo = await PublicKeyStoreService.FetchPublicKeyForMyMembership(
                baseAddress,
                cert);

            string version = null;
            foreach (var pubKey in publicKeyInfo)
            {
                try
                {
                    if (pubKey.KeyType == "signature") continue;

                    using var rsaPublic = RSA.Create();
                    using var rsaPublic2 = RSA.Create();

                    rsaPublic.ImportFromPem(pubKey.Key.ToCharArray());
                    rsaPublic2.ImportFromPem(GeneratedPublicKey.ToCharArray());

                    var der1 = rsaPublic.ExportSubjectPublicKeyInfo();
                    var der2 = rsaPublic2.ExportSubjectPublicKeyInfo();

                    bool equal = der1.SequenceEqual(der2);

                    if (equal)
                    {
                        version = pubKey.Version;
                    }
                }
                catch (Exception ex)
                {

                }
            }

            await UploadAndSaveToDatabase(version);

            GeneratedPublicKey = string.Empty;
            GeneratedPrivateKey = string.Empty;

            Application.Current.Dispatcher.Invoke(() =>
            {
                ((MainViewModel)Application.Current.MainWindow.DataContext).IsProcessing = false;
            });
        }

        private async Task UploadAndSaveToDatabase(string version = null)
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

            if (!string.IsNullOrEmpty(version))
            {
                SavePrivateKeyOnlyToDb(version);

                X509Certificate2 certificate;
                string baseAddress;
                (certificate, baseAddress) = GetCertificateAndBaseAddress();

                return;
            }

            // Need to upload public key //

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

            SavePrivateKeyOnlyToDb(currentVersion);

            // Call service to upload and activate the key (implement service logic)
            MessageBox.Show("Public key uploaded successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SavePrivateKeyOnlyToDb(string currentVersion)
        {
            var alreadyInDB = _dbContext.PrivateKeys.FirstOrDefault(x => x.Version == currentVersion);
            if (alreadyInDB != null)
            {
                MessageBox.Show($"Version {currentVersion} is already uploaded in the tool");
                return;
            }

            _dbContext.PrivateKeys.Add(new PrivateKey
            {
                Version = currentVersion,
                Key = GeneratedPrivateKey,
                IsActive = false,
                CreatedAt = DateTime.Now,
            });

            _dbContext.SaveChanges();
            PrivateKeys = new ObservableCollection<PrivateKey>(_dbContext.PrivateKeys.Select(pk => new PrivateKey
            {
                Id = pk.Id,
                Version = pk.Version,
                CreatedAt = pk.CreatedAt,
                Key = pk.Key,
                IsActive = pk.IsActive,
            }));
        }

        private async Task<bool> UploadPublicKey(string version)
        {
            X509Certificate2 certificate;
            string baseAddress;
            (certificate, baseAddress) = GetCertificateAndBaseAddress();

            var keyId = await PublicKeyStoreService.UploadPublicKey(baseAddress, GeneratedPublicKey, version, 365, "encryption", certificate);

            return true;
        }

        private async void ActivateEncryptionKey(PrivateKey privateKey)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ((MainViewModel)Application.Current.MainWindow.DataContext).IsProcessing = true;
            });

            (var cert, var baseAddress) = GetCertificateAndBaseAddress();

            if (cert == null) { return; }

            var allKeys = await PublicKeyStoreService.FetchPublicKeyForMyMembership(baseAddress, cert);

            var keyFromEcoHub = allKeys.Where(k => k.KeyType == "encryption")
                .FirstOrDefault(x => x.Version == privateKey.Version);

            if (keyFromEcoHub == null)
            {
                MessageBox.Show("PublicKey deleted from EcoHub, Can't be activated", "Error");
                return;
            }

            try
            {
                await PublicKeyStoreService.ValidateEncryptionKeyAsync(
                    baseAddress, 
                    keyFromEcoHub.KeyId.ToString(), 
                    cert,
                    privateKey.Key,
                    keyFromEcoHub.Key);
                await PublicKeyStoreService.ActivatePublicKey(baseAddress, keyFromEcoHub.KeyId.ToString(), cert);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ((MainViewModel)Application.Current.MainWindow.DataContext).IsProcessing = false;
                });
                return;
            }

            _dbContext.PrivateKeys.ToList().ForEach(x => x.IsActive = false);

            var signKeyFromDatabase = _dbContext.PrivateKeys.FirstOrDefault(x => x.Version == privateKey.Version);
            signKeyFromDatabase.IsActive = true;

            _dbContext.SaveChanges();
            PrivateKeys = new ObservableCollection<PrivateKey>(_dbContext.PrivateKeys.Select(pk => new PrivateKey
            {
                Id = pk.Id,
                Version = pk.Version,
                CreatedAt = pk.CreatedAt,
                Key = pk.Key,
                IsActive = pk.IsActive,
            }));

            Application.Current.Dispatcher.Invoke(() =>
            {
                ((MainViewModel)Application.Current.MainWindow.DataContext).IsProcessing = false;
            });

        }

        #endregion

        #region Signature key

        public ICommand GenerateSignatureKeyPairCommand { get; }
        public ICommand UploadSignatureKeyCommand { get; }
        public ICommand ActivateSignPublicKeyCommand { get; }

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

        private async Task ValidateAndUploadSignatureKey()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ((MainViewModel)Application.Current.MainWindow.DataContext).IsProcessing = true;
            });

            (var cert, var baseAddress) = GetCertificateAndBaseAddress();

            if (cert == null) { return; }

            // Fetch the public key
            var publicKeyInfo = await PublicKeyStoreService.FetchPublicKeyForMyMembership(
                baseAddress,
                cert);

            string version = null;
            // Prepare the reference public key once
            using var refEcdsa = ECDsa.Create();
            refEcdsa.ImportFromPem(GeneratedSignaturePublicKey);
            var refSpki = refEcdsa.ExportSubjectPublicKeyInfo();

            foreach (var pubKey in publicKeyInfo)
            {
                // Skip non-key entries if that's what you intended
                if (pubKey.KeyType.Equals("encryption", StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    using var ecdsa = ECDsa.Create();
                    ecdsa.ImportFromPem(pubKey.Key);

                    var spki = ecdsa.ExportSubjectPublicKeyInfo();

                    if (CryptographicOperations.FixedTimeEquals(spki, refSpki))
                    {
                        version = pubKey.Version;
                        break; // stop at first match
                    }
                }
                catch (Exception ex)
                {

                }
                
            }


            await UploadSignatureKeyAndSaveToDatabase(version);
            GeneratedSignaturePublicKey = string.Empty;
            GeneratedSignaturePrivateKey = string.Empty;

            Application.Current.Dispatcher.Invoke(() =>
            {
                ((MainViewModel)Application.Current.MainWindow.DataContext).IsProcessing = false;
            });
        }



        private async Task UploadSignatureKeyAndSaveToDatabase(string version = null)
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

            if (!string.IsNullOrEmpty(version))
            {
                SaveSignaturePrivateKeyOnlyToDb(version);

                X509Certificate2 certificate;
                string baseAddress;
                (certificate, baseAddress) = GetCertificateAndBaseAddress();

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

            SaveSignaturePrivateKeyOnlyToDb(currentVersion);

            // Call service to upload and activate the key (implement service logic)
            MessageBox.Show("Public key uploaded successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SaveSignaturePrivateKeyOnlyToDb(string currentVersion)
        {
            var alreadyInDB = _dbContext.SignatureKeys.FirstOrDefault(x => x.Version == currentVersion);
            if (alreadyInDB != null)
            {
                MessageBox.Show($"Version {currentVersion} is already uploaded in the tool");
                return;
            }

            _dbContext.SignatureKeys.Add(new SignatureKey
            {
                Version = currentVersion,
                Key = GeneratedSignaturePrivateKey,
                IsActive = false,
                CreatedAt = DateTime.Now,
            });

            _dbContext.SaveChanges();
            SignatureKeys = new ObservableCollection<SignatureKey>(_dbContext.SignatureKeys.Select(pk => new SignatureKey
            {
                Id = pk.Id,
                Version = pk.Version,
                CreatedAt = pk.CreatedAt,
                Key = pk.Key,
                IsActive = pk.IsActive,
            }));
        }

        private async Task<bool> UploadSignaturePublicKey(string version)
        {
            (var cert, var baseAddress) = GetCertificateAndBaseAddress();

            if (cert == null) { return false; }
            var keyId = await PublicKeyStoreService.UploadPublicKey(
                baseAddress, 
                GeneratedSignaturePublicKey, 
                version, 
                365, 
                "signature", 
                cert);
            return true;
        }

        // Example ActivateKeyCommand
        public ICommand ActivateEncPublicKeyCommand { get; }

        private async void ActivateSignatureKey(SignatureKey signatureKey)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ((MainViewModel)Application.Current.MainWindow.DataContext).IsProcessing = true;
            });

            (var cert, var baseAddress) = GetCertificateAndBaseAddress();

            if (cert == null) { return; }

            var allKeys = await PublicKeyStoreService.FetchPublicKeyForMyMembership(baseAddress, cert);

            var keyFromEcoHub = allKeys.Where(k => k.KeyType == "signature")
                .FirstOrDefault(x => x.Version == signatureKey.Version);
            
            if(keyFromEcoHub == null) {
                MessageBox.Show("PublicKey deleted from EcoHub, Can't be activated", "Error");
                return; }

            try
            {
                await PublicKeyStoreService.ValidateSignatureKeyAsync(
                    baseAddress,
                    keyFromEcoHub.KeyId.ToString(),
                    cert,
                    signatureKey.Key,
                    keyFromEcoHub.Key);
                await PublicKeyStoreService.ActivatePublicKey(baseAddress, keyFromEcoHub.KeyId.ToString(), cert);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ((MainViewModel)Application.Current.MainWindow.DataContext).IsProcessing = false;
                });
                return;
            }

            _dbContext.SignatureKeys.ToList().ForEach(x => x.IsActive = false);

            var signKeyFromDatabase = _dbContext.SignatureKeys.FirstOrDefault(x => x.Version == signatureKey.Version);
            signKeyFromDatabase.IsActive = true;

            _dbContext.SaveChanges();
            SignatureKeys = new ObservableCollection<SignatureKey>(_dbContext.SignatureKeys.Select(pk => new SignatureKey
            {
                Id = pk.Id,
                Version = pk.Version,
                CreatedAt = pk.CreatedAt,
                Key = pk.Key,
                IsActive = pk.IsActive,
            }));

            Application.Current.Dispatcher.Invoke(() =>
            {
                ((MainViewModel)Application.Current.MainWindow.DataContext).IsProcessing = false;
            });

        }


        #endregion

        #region Common private methods

        private (X509Certificate2, string) GetCertificateAndBaseAddress()
        {
            X509Certificate2 certificate;
            string baseAddress;
            var profile = _dbContext.Profiles.FirstOrDefault();
            if (profile == null)
            {
                MessageBox.Show("Profile information is missing. Please check your general settings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return (null, null);
            }

            var environment = _dbContext.EnvironmentSettings.FirstOrDefault(e => e.EnvironmentName == profile.SelectedEnvironment);
            if (environment == null)
            {
                MessageBox.Show("Environment not found. Please check your general settings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return (null, null);
            }


            certificate = GetClientCertificate();
            baseAddress = environment.ServicesApiUrl;

            return (certificate, baseAddress);
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


        #endregion
    }
}
