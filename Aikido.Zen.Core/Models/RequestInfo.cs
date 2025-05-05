using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Aikido.Zen.Core.Models
{
    public class RequestInfo
    {
        [JsonPropertyName("method")]
        public string Method { get; set; }
        [JsonPropertyName("ipAddress")]
        public string IpAddress { get; set; }
        [JsonPropertyName("userAgent")]
        public string UserAgent { get; set; }
        [JsonPropertyName("url")]
        public string Url { get; set; }
        [JsonPropertyName("headers")]
        public IDictionary<string, string> Headers { get; set; }
        [JsonPropertyName("body")]
        public string Body { get; set; }
        [JsonPropertyName("source")]
        public string Source { get; set; }
        [JsonPropertyName("route")]
        public string Route { get; set; }
    }
}
