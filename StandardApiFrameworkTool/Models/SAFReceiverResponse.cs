using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StandardApiFrameworkTool.Models
{
    public class SAFReceiverResponse
    {
        public List<string> Idp { get; set; }

        public string CompanyName { get; set; }

        public string MemberType { get; set; }
        //public List<SAFSupportedStandard> SafSupportedStandards { get; set; }
    }
}
