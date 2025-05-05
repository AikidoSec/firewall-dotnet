using System.Text.Json.Serialization;

namespace Aikido.Zen.Core.Models
{

    public class RateLimitingConfig
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }
        [JsonPropertyName("maxRequests")]
        public int MaxRequests { get; set; }
        [JsonPropertyName("windowSizeInMS")]
        public int WindowSizeInMS { get; set; }
    }
}
