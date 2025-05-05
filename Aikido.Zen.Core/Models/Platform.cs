using System.Text.Json.Serialization;

namespace Aikido.Zen.Core.Models
{

    public class Platform
    {
        [JsonPropertyName("version")]
        public string Version { get; set; }
        [JsonPropertyName("arch")]
        public string Arch { get; set; }
    }
}
