using Newtonsoft.Json;
using StandardApiFrameworkTool.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using StandardApiFrameworkTool.Helpers;

namespace StandardApiFrameworkTool.Services
{
    public static class TechUserService
    {
        public static async Task<TechUserResponse> EnrolTechUser(
            string iak,
            string password,
            string idpNumber,
            string licenceKey,
            string baseAddress)
        {
            // Initialize HttpClient
            var client = new HttpClient();
            client.BaseAddress = new Uri(baseAddress);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            try
            {
                var requestData = new
                {
                    iak = iak,
                    idpUserId = idpNumber,
                    licenceKey = licenceKey,
                    password = password,
                    requestId = Guid.NewGuid().ToString(),
                    requestTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    userAgent = new
                    {
                        name = "Client software (TEST)",
                        version = "Version 1.0"
                    },
                };

                // Serialize request data to JSON
                var jsonRequest = JsonConvert.SerializeObject(requestData);

                // Send HTTP POST request
                var response = await client.PostAsync("general/v1/techUserEnrolment", new StringContent(jsonRequest, Encoding.UTF8, "application/json"));

                // Handle response
                if (response.IsSuccessStatusCode)
                {
                    // Read response content as string
                    string jsonResponse = await response.Content.ReadAsStringAsync();


                    var tuResponse = JsonConvert.DeserializeObject<TechUserResponse>(jsonResponse);

                    return tuResponse;
                }
                else
                {
                    UiServices.ShowError($"Tech User Enrolment - Status Code {response.StatusCode}, " +
                        $"Message: {await response.Content.ReadAsStringAsync()}", "Error");
                    return null;
                }
            }
            catch (Exception ex)
            {
                UiServices.ShowError($"An error occurred: {ex.Message}", "Error");
                return null;
            }
        }

        public static async Task<List<SAFReceiverResponse>> GetReceiver(
            string licenceKey,
            string password,
            string baseAddress,
            X509Certificate2 x509Certificate2)
        {
            try
            {

                // Fetch the public key using mTLS (SAF Client Certificate)
                HttpClientHandler handler = new HttpClientHandler();
                handler.ClientCertificateOptions = ClientCertificateOption.Manual;
                handler.SslProtocols = System.Security.Authentication.SslProtocols.Tls12;

                // Load the SAF client certificate
                handler.ClientCertificates.Add(x509Certificate2);

                using (HttpClient client = new HttpClient(handler))
                {

                    var requestData = new
                    {
                        licenceKey = licenceKey,
                        password = password,
                        requestId = Guid.NewGuid().ToString(),
                        requestTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                        userAgent = new
                        {
                            name = "Client software (TEST)",
                            version = "Version 1.0"
                        }
                    };

                    // Serialize request data to JSON
                    var jsonRequest = JsonConvert.SerializeObject(requestData);

                    // Send HTTP POST request
                    var response = await client.PostAsync($"{baseAddress}/general/v1/saf-receivers", new StringContent(jsonRequest, Encoding.UTF8, "application/json"));

                    // Handle response
                    if (response.IsSuccessStatusCode)
                    {
                        // Read response content as string
                        string jsonResponse = await response.Content.ReadAsStringAsync();

                        var receiverResponse = JsonConvert.DeserializeObject<List<SAFReceiverResponse>>(jsonResponse);

                        return receiverResponse;
                    }
                    else
                    {
                        UiServices.ShowError($"Get Receiver - Status Code {response.StatusCode}, " +
                            $"Message: {await response.Content.ReadAsStringAsync()}", "Error");
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                UiServices.ShowError($"An error occurred: {ex.Message}", "Error");
                return null;
            }
        }
    }
}
