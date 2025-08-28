using Newtonsoft.Json;

namespace StandardApiFrameworkTool.Models
{
    public class PublicKeyInfo
    {
        [JsonProperty("keyType")]
        public string KeyType { get; set; }

        [JsonProperty("supportedProcesses")]
        public List<SupportedProcess> SupportedProcesses { get; set; }

        [JsonProperty("keyId")]
        public Guid KeyId { get; set; }

        [JsonProperty("membershipId")]
        public Guid MembershipId { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        // PEM (public key)
        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("createdAt")]
        public DateTimeOffset CreatedAt { get; set; }

        [JsonProperty("lastUpdatedAt")]
        public DateTimeOffset LastUpdatedAt { get; set; }

        [JsonProperty("activatedAt")]
        public DateTimeOffset ActivatedAt { get; set; }

        [JsonProperty("expiryDate")]
        public DateTimeOffset ExpiryDate { get; set; }

        [JsonProperty("ecoHubStatus")]
        public string EcoHubStatus { get; set; }

        [JsonProperty("verificationStatus")]
        public string VerificationStatus { get; set; }
    }

    public class SupportedProcess
    {
        [JsonProperty("processName")]
        public string ProcessName { get; set; }
    }
}
