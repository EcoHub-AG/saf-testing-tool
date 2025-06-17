namespace StandardApiFrameworkTool.Models
{
    public class PublicKeyInfo
    {
        public string KeyId { get; set; }
        public Guid MembershipId { get; set; }
        public string version { get; set; }
        public string Key { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }
        public DateTime ActivatedAt { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string EcoHubStatus { get; set; }
    }
}
