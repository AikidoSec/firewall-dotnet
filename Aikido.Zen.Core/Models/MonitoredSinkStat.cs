using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Aikido.Zen.Core.Models
{
    public class OperationStats
    {
        [JsonPropertyName("attacksDetected")]
        public AttacksDetected AttacksDetected { get; set; }
        [JsonPropertyName("interceptorThrewError"), JsonInclude]
        public long InterceptorThrewError; // must be a field to be thread safe
        [JsonPropertyName("withoutContext"), JsonInclude]
        public long WithoutContext; // must be a field to be thread safe
        [JsonPropertyName("total"), JsonInclude]
        public long Total; // must be a field to be thread safe
        [JsonPropertyName("compressedTimings")]
        public IList<CompressedTiming> CompressedTimings { get; set; }
        [JsonIgnore] // we do not send durations to the server, we send compressed timings instead
        public IList<double> Durations { get; set; }
        [JsonPropertyName("kind")]
        public string Kind { get; set; }
        [JsonPropertyName("operation")]
        public string Operation { get; set; }
    }

}
