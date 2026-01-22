using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StandardApiFrameworkTool.Helpers;
using StandardApiFrameworkTool.Models;
using StandardApiFrameworkTool.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Xml.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
            SelectedStandard = "offer.nlpi";
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
            UiServices.SetIsProcessing(true);

            try
            {
                var profile = _dbContext.Profiles.FirstOrDefault();
                if (profile == null)
                {
                    UiServices.ShowError("Not connected. Please configure general settings.", "Error");
                    return;
                }

                var certificate = GetClientCertificate();

                var environment = _dbContext.EnvironmentSettings.FirstOrDefault(e => e.EnvironmentName == profile.SelectedEnvironment);
                if (environment == null)
                {
                    UiServices.ShowError("Environment information is missing.", "Error");
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
                    UiServices.ShowInfo("No receivers found.", "Info");
                }
            }
            catch (Exception ex)
            {
                UiServices.ShowError($"An error occurred while loading receivers: {ex.Message}", "Error");
            }

            UiServices.SetIsProcessing(false);
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

        private string _signatureContent;

        public string SignatureContent
        {
            get { return _signatureContent; }
            set { _signatureContent = value; OnPropertyChanged(nameof(SignatureContent)); }
        }

        private string _messageHash;

        public string MessageHash
        {
            get { return _messageHash; }
            set { _messageHash = value; OnPropertyChanged(nameof(MessageHash)); }
        }

        public ObservableCollection<string> Standards { get; } =
                new ObservableCollection<string>
                {
            "offer.nlpi",
            "Invoices",
            "Contract",
            "Commission"
                };

        public ObservableCollection<string> Versions { get; } =
            new ObservableCollection<string>();

        private string _selectedStandard;
        public string SelectedStandard
        {
            get => _selectedStandard;
            set
            {
                if (_selectedStandard != value)
                {
                    _selectedStandard = value;
                    OnPropertyChanged(nameof(SelectedStandard));
                    UpdateVersions();   // 🔥 update versions dynamically
                }
            }
        }

        private string _selectedVersion;
        public string SelectedVersion
        {
            get => _selectedVersion;
            set
            {
                _selectedVersion = value;
                OnPropertyChanged(nameof(SelectedVersion));
            }
        }

        private void UpdateVersions()
        {
            Versions.Clear();

            if (SelectedStandard == "offer.nlpi")
            {
                Versions.Add("1.0.0");
            }
            else
            {
                Versions.Add("5.2.1");
                Versions.Add("5.4.1");
            }

            // Optional: Auto-select first version
            SelectedVersion = Versions.FirstOrDefault();
        }


        // methods

        private async Task FetchAndEncryptContentAsync()
        {
            UiServices.SetIsProcessing(true);
            try
            {
                var profile = _dbContext.Profiles.FirstOrDefault();
                if (profile == null)
                {
                    UiServices.ShowError("Profile information is missing. Please check your general settings.", "Error");
                    return;
                }

                var environment = _dbContext.EnvironmentSettings.FirstOrDefault(e => e.EnvironmentName == profile.SelectedEnvironment);
                if (environment == null)
                {
                    UiServices.ShowError("Environment not found. Please check your general settings.", "Error");
                    return;
                }

                if (string.IsNullOrWhiteSpace(InputContent))
                {
                    UiServices.ShowError("Please enter content to encrypt.", "Error");
                    return;
                }

                var certificate = GetClientCertificate();

                if(SelectedReceiver == null) 
                {
                    UiServices.ShowError("Please select a receiver first from step 1", "Error");
                    return;
                }

                // Fetch the public key
                var publicKeyInfo = await PublicKeyStoreService.FetchPublicKey(
                    environment.ServicesApiUrl, 
                    SelectedReceiver.Idp.FirstOrDefault(), 
                    certificate);

                if (publicKeyInfo == null || publicKeyInfo.Count == 0)
                {
                    UiServices.ShowError("Failed to retrieve a valid public key.", "Error");

                    UiServices.SetIsProcessing(false);

                    return;
                }

                var hasEncKey = publicKeyInfo
                    .Where(p => p.SupportedProcesses == null || p.SupportedProcesses.Any(x => x.ProcessName == SelectedStandard))
                    .Where(p => p.EcoHubStatus == "Activated")
                    .Where(p => p.KeyType == "encryption")
                    .Any();

                if (!hasEncKey)
                {
                    UiServices.ShowError("Failed to retrieve a valid encryption public key.", "Error");

                    UiServices.SetIsProcessing(false);

                    return;
                }

                var encKey = publicKeyInfo
                    .Where(p => p.SupportedProcesses == null || p.SupportedProcesses.Any(x => x.ProcessName == SelectedStandard))
                    .Where(p => p.EcoHubStatus == "Activated")
                    .Where(p => p.KeyType == "encryption")
                    .FirstOrDefault();

                // Encrypt the content
                EncryptContentWithPublicKey(encKey.Key);

                SignPayload();
            }
            catch (Exception ex)
            {
                UiServices.ShowError($"An error occurred: {ex.Message}", "Error");
            }

            UiServices.SetIsProcessing(false);
        }

        private void SignPayload()
        {
            // Manually hash the message
            byte[] hashBytes;
            using (var sha384 = SHA384.Create())
            {
                hashBytes = sha384.ComputeHash(Encoding.UTF8.GetBytes(EncryptedContent));
            }
            MessageHash = Convert.ToBase64String(hashBytes);

            var signKey = _dbContext.SignatureKeys.FirstOrDefault(s => s.IsActive);

            if(signKey == null)
            {
                UiServices.ShowError("No Signing Key found.");
                return;
            }

            // Load private key
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(signKey.Key.ToCharArray());

            // Convert payload to bytes
            byte[] payloadBytes = Encoding.UTF8.GetBytes(EncryptedContent);

            // Hash and sign the data
            byte[] signature = ecdsa.SignData(payloadBytes, HashAlgorithmName.SHA384, DSASignatureFormat.Rfc3279DerSequence);

            
            SignatureContent =  Convert.ToBase64String(signature);
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

                byte[] encryptedAesKey = rsa.Encrypt(aesKey, RSAEncryptionPadding.OaepSHA256);

                EncryptedAESKey = Convert.ToBase64String(encryptedAesKey);

                // Zip and encrypt the content
                byte[] zippedContent = ZipContent(InputContent);
                byte[] encryptedContent = EncryptWithAES(zippedContent, aesKey);

                EncryptedContent = Convert.ToBase64String(encryptedContent);
            }
            catch (Exception ex)
            {
                UiServices.ShowError($"Error encrypting content: {ex.Message}", "Error");
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
                UiServices.ShowError("Profile information is missing. Please check your general settings.", "Error");
                return;
            }

            var environment = _dbContext.EnvironmentSettings.FirstOrDefault(e => e.EnvironmentName == profile.SelectedEnvironment);
            if (environment == null)
            {
                UiServices.ShowError("Environment not found. Please check your general settings.", "Error");
                return;
            }

            if (string.IsNullOrWhiteSpace(InputContent))
            {
                UiServices.ShowError("Please enter content to encrypt.", "Error");
                return;
            }

            var certificate = GetClientCertificate();

            if (SelectedReceiver == null)
            {
                UiServices.ShowError("Please select a receiver first from step 1", "Error");
                return;
            }

            // Fetch the public key
            var publicKeyInfo = await PublicKeyStoreService.FetchPublicKey(
                environment.ServicesApiUrl,
                SelectedReceiver.Idp.FirstOrDefault(),
                certificate);

            if (publicKeyInfo == null || publicKeyInfo.Count == 0)
            {
                UiServices.ShowError("Failed to retrieve a valid public key.", "Error");

                UiServices.SetIsProcessing(false);

                return;
            }

            var hasEncKey = publicKeyInfo
                .Where(p => p.SupportedProcesses == null || p.SupportedProcesses.Any(x => x.ProcessName == "offer.nlpi"))
                .Where(p => p.EcoHubStatus == "Activated")
                .Where(p => p.KeyType == "encryption")
                .Any();

            if (!hasEncKey)
            {
                UiServices.ShowError("Failed to retrieve a valid encryption public key.", "Error");

                UiServices.SetIsProcessing(false);

                return;
            }

            var encKey = publicKeyInfo
                .Where(p => p.SupportedProcesses == null || p.SupportedProcesses.Any(x => x.ProcessName == SelectedStandard))
                .Where(p => p.EcoHubStatus == "Activated")
                .Where(p => p.KeyType == "encryption")
                .FirstOrDefault();


            // Initialize the model with the values directly
            var offerNLPIEvent = new CommonEventType
            {
                Id = Guid.NewGuid().ToString(),
                Source = "http://www.myecohub.ch/",
                Specversion = "1.0",
                Type = "ch.ecohub.saf.data",
                DataContentType = "application/json",
                DataSchema = "https://raw.githubusercontent.com/EcoHub-AG/Standards/refs/tags/Offer_NLPI_v0.3.0/schemas/Offer-NLPI/v0.3.0/offer-nlpi-root/OfferNlpiRequestDataType.json",
                Subject = "Test subject",
                Time = DateTime.UtcNow.ToString("yyyy-MM-ddThh:mm:ss.fffZ"),
                Data = new Data
                {
                    Payload = EncryptedContent,
                    Links = new List<Links>(), // Empty list, as per the provided JSON
                    EncryptionKey = EncryptedAESKey,
                    PublicKeyVersion = encKey.Version,
                    PayloadSignature = SignatureContent,
                    SignatureKeyVersion = _dbContext.SignatureKeys.FirstOrDefault(s => s.IsActive)?.Version,
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
                ProcessId = GetProcessId(InputContent) ?? Guid.NewGuid().ToString(),
                ProcessGroupId = Guid.NewGuid().ToString(),
                ProcessStatus = SelectedStandard == "offer.nlpi"?  "active" : "closed",
                SubProcessName = "request",
                ProcessName = SelectedStandard,
                SubProcessStatus = "Created",
                ProcessVersion = SelectedVersion,
            };


            // Serialize the object and format it with indentation
            string jsonString = JsonConvert.SerializeObject(offerNLPIEvent, Formatting.Indented);


            PayloadContent = jsonString;
        }

        private string GetProcessId(string data)
        {
            if (data == null || data.Length == 0)
                return null;

            try
            {
                // Convert byte array to string
                string xmlContent = data;

                // Load XML
                XDocument doc = XDocument.Parse(xmlContent);

                // Locate <header> element, namespace-agnostic
                var header = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "header");
                if (header == null)
                    return null;

                // Extract values ignoring namespace
                string? processId = header.Descendants().FirstOrDefault(e => e.Name.LocalName == "identificationNo")?.Value;
               
                return processId;
            }
            catch
            {
                return null;
            }
        }


        public ICommand SendPayloadCommand { get; }

        public async Task SendPayload()
        {
            var profile = _dbContext.Profiles.FirstOrDefault();
            if (profile == null)
            {
                UiServices.ShowError("Not connected. Please configure general settings.", "Error");
                return;
            }

            // Retrieve environment settings from the database
            var env = _dbContext.EnvironmentSettings.FirstOrDefault(e => e.EnvironmentName == profile.SelectedEnvironment);

            if (env == null)
            {
                UiServices.ShowError("Environment settings not found!", "Error");
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
            string topic = "eh.saf.in.v1";  // The topic you want to send the message to
            

            // Kafka producer configuration
            var config = new ProducerConfig
            {
                BootstrapServers = bootstrapServers,
                SecurityProtocol = SecurityProtocol.Ssl,
                SslCertificatePem = publicKeyPem,
                SslKeyPem = privateKeyPem,
                //SslCaPem = caPem, // Uncomment if you need to use the CA PEM
            };

            var techUser = _dbContext.TechUsers.FirstOrDefault();
            // We have already checked that techUser is not null in GetClientCertificate();


            var pfxBytes = Convert.FromBase64String(techUser.TechUserCert);   // your string
            var pfxPath = Path.Combine(Path.GetTempPath(),
                                        $"{Guid.NewGuid():N}.pfx");
            File.WriteAllBytes(pfxPath, pfxBytes);

            var schemaRegistryConfig = new SchemaRegistryConfig
            {
                Url = env.ServicesApiUrl + "/schemaregistry",
                SslKeystoreLocation = pfxPath,
                SslKeystorePassword = _dbContext.Profiles.FirstOrDefault().Password,
            };


            try
            {
                // Parse the JSON input
                var myEvent = JsonConvert.DeserializeObject<CommonEventType>(PayloadContent);

                var schemaRegistry = new CachedSchemaRegistryClient(schemaRegistryConfig);

                // Create the Kafka producer
                using var producer = new ProducerBuilder<ProcessIdType, JObject>(config)
                    .SetValueSerializer(new JsonSerializer<JObject>(schemaRegistry, new JsonSerializerConfig
                    {
                        BufferBytes = 100,
                        UseLatestVersion = true,
                        AutoRegisterSchemas = false,
                        SubjectNameStrategy = SubjectNameStrategy.Topic,
                    }))
                    .SetKeySerializer(new JsonSerializer<ProcessIdType>(schemaRegistry, new JsonSerializerConfig()
                    {
                        UseLatestVersion = true,
                        AutoRegisterSchemas = false,
                    }))
                    .Build();

                // Create a message to send
                var message = new Message<ProcessIdType, JObject>
                {
                    Key = new ProcessIdType { ProcessId = Guid.Parse(myEvent.ProcessId) },
                    Value = JObject.Parse(PayloadContent)
                };

                // Send the message to the Kafka topic
                var deliveryReport = await producer.ProduceAsync(topic, message);

                UiServices.ShowInfo($"Message sent to topic {topic}. Offset: {deliveryReport.Offset}", "Success");
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                UiServices.ShowError($"Invalid JSON format: {ex.Message}", "Error");
            }
            catch (ProduceException<ProcessIdType, JObject> ex)
            {
                UiServices.ShowError($"Kafka error: {ex.Error.Reason}", "Error");
            }
            catch (Exception ex)
            {
                UiServices.ShowError($"An error occurred: {ex.Message}", "Error");
            }
        }

        #endregion
    }
}
