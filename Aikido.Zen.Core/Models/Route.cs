namespace Aikido.Zen.Core.Models
{
    /// <summary>
    /// Represents a route with path, method, and API specification details.
    /// Inherits from HitCount to track usage for LFU eviction.
    /// </summary>
    public class Route : HitCount
    {
        public string Path { get; set; }
        public string Method { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("apispec")]
        public APISpec ApiSpec { get; set; }

        public Route() : base() { }
    }
}
