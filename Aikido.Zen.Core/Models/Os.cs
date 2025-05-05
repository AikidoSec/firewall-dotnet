using System.Text.Json.Serialization;

namespace Aikido.Zen.Core.Models
{

    public class Os
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("version")]
        public string Version { get; set; }
    }
}
