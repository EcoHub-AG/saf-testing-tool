using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StandardApiFrameworkTool.Models
{
    public class ProcessIdType
    {
        [JsonProperty("processId", NullValueHandling = NullValueHandling.Ignore)]
        public Guid ProcessId { get; set; }
    }
}
