using System.Text.Json.Serialization;

namespace Aikido.Zen.Core.Models
{
    public class AttacksDetected
    {
        [JsonPropertyName("total"), JsonInclude]
        public int Total; // must be a field to be thread safe
        [JsonPropertyName("blocked"), JsonInclude]
        public int Blocked; // must be a field to be thread safe
    }
}
