namespace Aikido.Zen.Core.Models
{
    public class Route : HitCount
    {
        public string Path { get; set; }
        public string Method { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("apispec")]
        public APISpec ApiSpec { get; set; }
    }
}
