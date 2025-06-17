using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StandardApiFrameworkTool.Models
{
    public class TechUserResponse
    {
        public string techUserCert { get; set; }
        public OAuth2Settings oAuth2 { get; set; }
    }

    public class OAuth2Settings
    {
        public string clientId { get; set; }
        public string clientSecret { get; set; }
        public string openIdConfigurationEndpoint { get; set; }
    }
}
