using System.Text.Json.Serialization;

namespace Aikido.Zen.Core.Models
{
    public class Requests
    {
        [JsonPropertyName("total"), JsonInclude]
        public int Total; // must be a field to be thread safe
        [JsonPropertyName("aborted"), JsonInclude]
        public int Aborted; // must be a field to be thread safe
        [JsonPropertyName("attacksDetected"), JsonInclude]
        public AttacksDetected AttacksDetected { get; set; }
    }
}
