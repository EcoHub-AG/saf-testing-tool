using Newtonsoft.Json;
using StandardApiFrameworkTool.Exceptions;
using StandardApiFrameworkTool.Models;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace StandardApiFrameworkTool.Services
{
    public static class PublicKeyStoreService
    {
        public static async Task<List<PublicKeyInfo>> FetchPublicKey(
            string baseAddress,
            string idpNumber,
            X509Certificate2 certificate)
        {
            string apiUrl = $"{baseAddress}/publickeystore/v1/members/{idpNumber}/keys";

            HttpClientHandler handler = new HttpClientHandler
            {
                ClientCertificateOptions = ClientCertificateOption.Manual,
                SslProtocols = System.Security.Authentication.SslProtocols.Tls12
            };

            handler.ClientCertificates.Add(certificate);

            using (HttpClient client = new HttpClient(handler))
            {
                HttpResponseMessage response = await client.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<List<PublicKeyInfo>>(responseBody);
                }
                else
                {
                    throw new HttpRequestException($"Failed to fetch public key. Status code: {response.StatusCode}");
                }
            }
        }

        public static async Task<List<PublicKeyInfo>> FetchPublicKeyForMyMembership(
            string baseAddress,
            X509Certificate2 certificate)
        {
            string apiUrl = $"{baseAddress}/publickeystore/v1/keys";

            HttpClientHandler handler = new HttpClientHandler
            {
                ClientCertificateOptions = ClientCertificateOption.Manual,
                SslProtocols = System.Security.Authentication.SslProtocols.Tls12
            };

            handler.ClientCertificates.Add(certificate);

            using (HttpClient client = new HttpClient(handler))
            {
                HttpResponseMessage response = await client.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<List<PublicKeyInfo>>(responseBody);
                }
                else
                {
                    throw new HttpRequestException($"Failed to fetch public key. Status code: {response.StatusCode}");
                }
            }
        }

        public static async Task<Guid> UploadPublicKey(
            string baseAddress,
            string key,
            string version,
            int expireInDays,
            string keyType,
            X509Certificate2 certificate)
        {
            string apiUrl = $"{baseAddress}/publickeystore/v1/keys";

            HttpClientHandler handler = new HttpClientHandler
            {
                ClientCertificateOptions = ClientCertificateOption.Manual,
                SslProtocols = System.Security.Authentication.SslProtocols.Tls12
            };

            var reqBody = new
            {
                version,
                key,
                expireInDays,
                keyType
            };
            var content = new StringContent(JsonConvert.SerializeObject(new List<object>() { reqBody }), System.Text.Encoding.UTF8, "application/json");

            handler.ClientCertificates.Add(certificate);

            using (HttpClient client = new HttpClient(handler))
            {
                HttpResponseMessage response = await client.PostAsync(apiUrl, content);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var pkiDetails = JsonConvert.DeserializeObject<List<PublicKeyDetails>>(responseBody);
                    return pkiDetails.FirstOrDefault().KeyId;
                }
                else
                {
                    using (JsonDocument doc = JsonDocument.Parse(responseBody))
                    {
                        // Access the "errorCode" property
                        if (doc.RootElement.TryGetProperty("errorCode", out JsonElement errorCodeElement))
                        {
                            string errorCode = errorCodeElement.GetString();
                            if(errorCode == "KEY_VERSION_EXISTS") 
                            {
                                throw new KeyVersionExists();
                            }
                        }
                    }
                    
                    throw new HttpRequestException($"Failed to upload public key. Status code: {response.StatusCode}");
                }
            }
        }

        public static async Task ActivatePublicKey(
            string baseAddress,
            string keyId,
            X509Certificate2 certificate)
        {
            string apiUrl = $"{baseAddress}/publickeystore/v1/keys/{keyId}/activate";

            HttpClientHandler handler = new HttpClientHandler
            {
                ClientCertificateOptions = ClientCertificateOption.Manual,
                SslProtocols = System.Security.Authentication.SslProtocols.Tls12
            };

            handler.ClientCertificates.Add(certificate);

            using (HttpClient client = new HttpClient(handler))
            {
                HttpResponseMessage response = await client.PostAsync(apiUrl, null);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return;
                }
                else
                {
                    throw new HttpRequestException($"Failed to activate public key. Status code: {response.StatusCode}, {responseBody}");
                }
            }
        }

        public static async Task ValidateEncryptionKeyAsync(
            string baseAddress,
            string keyId,
            X509Certificate2 certificate,
            string privateKeypem,
            string publicKeyPem)
        {
            string verifyUrl = $"{baseAddress}/publickeystore/v1/keys/{keyId}/verify";

            using var client = CreateMtlsClient(certificate);

            // GET challenge
            string verificationContent = await GetVerificationContentAsync(client, verifyUrl);

            // Decrypt with RSA private key
            using RSA rsa = RSA.Create();
            rsa.ImportFromPem(privateKeypem);
            byte[] cipher = Convert.FromBase64String(verificationContent);
            byte[] plain = rsa.Decrypt(cipher, RSAEncryptionPadding.OaepSHA256);
            string verifiedContent = Encoding.UTF8.GetString(plain);

            // POST back
            var payload = new { keyId, verifiedContent };
            using var resp = await client.PostAsJsonAsync(verifyUrl, payload);
            resp.EnsureSuccessStatusCode();
        }

        public static async Task ValidateSignatureKeyAsync(
            string baseAddress,
            string keyId,
            X509Certificate2 certificate,
            string privateKeypem,
            string publicKeyPem)
        {
            string verifyUrl = $"{baseAddress}/publickeystore/v1/keys/{keyId}/verify";

            using var client = CreateMtlsClient(certificate);

            // GET challenge
            string verificationContent = await GetVerificationContentAsync(client, verifyUrl);

            // Sign with ECDSA private key
            using ECDsa ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(privateKeypem);
            byte[] data = Encoding.UTF8.GetBytes(verificationContent);
            byte[] signature = ecdsa.SignData(data, HashAlgorithmName.SHA384, DSASignatureFormat.Rfc3279DerSequence);
            string verifiedContent = Convert.ToBase64String(signature);

            // POST back
            var payload = new { keyId, verifiedContent };
            using var resp = await client.PostAsJsonAsync(verifyUrl, payload);
            resp.EnsureSuccessStatusCode();
        }


        private static HttpClient CreateMtlsClient(X509Certificate2 certificate)
        {
            var handler = new HttpClientHandler
            {
                ClientCertificateOptions = ClientCertificateOption.Manual,
                SslProtocols = System.Security.Authentication.SslProtocols.Tls12
            };
            handler.ClientCertificates.Add(certificate);
            return new HttpClient(handler);
        }

        private static async Task<string> GetVerificationContentAsync(HttpClient client, string verifyUrl)
        {
            string body = await client.GetStringAsync(verifyUrl);
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("verificationContent").GetString()!;
        }


    }
}
