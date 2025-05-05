using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Aikido.Zen.Core.Models
{
    public class Attack
    {
        [JsonPropertyName("kind")]
        public string Kind { get; set; }
        [JsonPropertyName("operation")]
        public string Operation { get; set; }
        [JsonPropertyName("module")]
        public string Module { get; set; }
        [JsonPropertyName("blocked")]
        public bool Blocked { get; set; }
        [JsonPropertyName("source")]
        public string Source { get; set; }
        [JsonPropertyName("path")]
        public string Path { get; set; }
        [JsonPropertyName("stack")]
        public string Stack { get; set; }
        [JsonPropertyName("payload")]
        public string Payload { get; set; }
        [JsonPropertyName("metadata")]
        public IDictionary<string, object> Metadata { get; set; }
        [JsonPropertyName("user")]
        public User User { get; set; }
    }
}
