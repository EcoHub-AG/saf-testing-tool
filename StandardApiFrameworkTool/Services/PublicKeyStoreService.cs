using Newtonsoft.Json;
using StandardApiFrameworkTool.Exceptions;
using StandardApiFrameworkTool.Models;
using System;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
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

                    throw new HttpRequestException($"Failed to activate public key. Status code: {response.StatusCode}");
                }
            }
        }
    }
}
