namespace Aikido.Zen.Core.Models
{
    public class Route
    {
        public string Path { get; set; }
        public string Method { get; set; }
        public int Hits { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("apispec")]
        public APISpec ApiSpec { get; set; }
    }
}
