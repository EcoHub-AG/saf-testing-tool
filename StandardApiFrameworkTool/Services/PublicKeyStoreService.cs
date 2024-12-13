using Newtonsoft.Json;
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
        public static async Task<PublicKeyInfo> FetchPublicKey(
            string baseAddress,
            string idpNumber,
            X509Certificate2 certificate)
        {
            string apiUrl = $"{baseAddress}/publickeystore/members/{idpNumber}/key";

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
                    return JsonConvert.DeserializeObject<PublicKeyInfo>(responseBody);
                }
                else
                {
                    throw new HttpRequestException($"Failed to fetch public key. Status code: {response.StatusCode}");
                }
            }
        }
    }
}
