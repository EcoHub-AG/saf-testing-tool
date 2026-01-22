using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Media;

namespace StandardApiFrameworkTool.Models
{
    public class ThreadItem
    {
        public string Title { get; set; }
        public string Timestamp { get; set; }
        public string Payload { get; set; }
        public string Verified { get; set; }
        public string Label { get; set; }
        public IBrush LabelColor { get; set; }
    }

}
