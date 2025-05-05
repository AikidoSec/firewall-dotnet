using System.Text.Json.Serialization;

namespace Aikido.Zen.Core.Models
{
    /// <summary>
    /// Represents a route with path, method, and API specification details.
    /// Inherits from HitCount to track usage for LFU eviction.
    /// </summary>
    public class Route : HitCount
    {
        [JsonPropertyName("path")]
        public string Path { get; set; }
        [JsonPropertyName("method")]
        public string Method { get; set; }

        [JsonPropertyName("apispec")]
        public APISpec ApiSpec { get; set; }

        public Route() : base() { }
    }
}
