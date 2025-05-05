using System.Collections.Generic;
using System.Text.Json.Serialization;
namespace Aikido.Zen.Core.Models
{
    public class CompressedTiming
    {
        [JsonPropertyName("averageInMS")]
        public double AverageInMS { get; set; }
        [JsonPropertyName("percentiles")]
        public Dictionary<string, double> Percentiles { get; set; } = new Dictionary<string, double>();
        [JsonPropertyName("compressedAt")]
        public long CompressedAt { get; set; }
    }
}
