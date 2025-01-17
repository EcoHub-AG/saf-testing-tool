using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StandardApiFrameworkTool.Models
{
    public class PublicKeyDetails
    {
        public Guid KeyId { get; set; }
        public Guid MembershipId { get; set; }
        public string Version { get; set; }
        public string Key { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset LastUpdatedAt { get; set; }
        public DateTimeOffset ActivatedAt { get; set; }
        public DateTimeOffset ExpiryDate { get; set; }
    }
}
