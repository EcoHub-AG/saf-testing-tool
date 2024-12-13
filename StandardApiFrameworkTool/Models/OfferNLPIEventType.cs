using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StandardApiFrameworkTool.Models
{
    public class Links
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Href { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Rel { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Description { get; set; }
    }

    public class Data
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Payload { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Md5PayloadHash { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<Links> Links { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string EncryptionKey { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string PublicKeyVersion { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Message { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Md5MessageHash { get; set; }
    }

    public class UserAgent
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Name { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Version { get; set; }
    }

    public class EventReceiver
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Category { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Id { get; set; }
    }

    public class EventSender
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Category { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Id { get; set; }
    }

    public class OfferNLPIEventType
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Id { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Source { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Specversion { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Type { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string DataContentType { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string DataSchema { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Subject { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Time { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Data Data { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string DataBase64 { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string LicenceKey { get; set; }

        public UserAgent UserAgent { get; set; }

        public EventReceiver EventReceiver { get; set; }

        public EventSender EventSender { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ProcessId { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ProcessStatus { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string SubProcessName { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string ProcessName { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string SubProcessStatus { get; set; }
    }
}
