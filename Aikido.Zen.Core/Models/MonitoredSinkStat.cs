using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Aikido.Zen.Core.Models
{
    public class OperationStats
    {
        public AttacksDetected AttacksDetected { get; set; }
        public int InterceptorThrewError { get; set; }
        public int WithoutContext { get; set; }
        public int Total { get; set; }
        public IList<CompressedTiming> CompressedTimings { get; set; }
        [JsonIgnore] // we do not send durations to the server, we send compressed timings instead
        public IList<double> Durations { get; set; }
        public string Kind { get; set; }
        public string Operation { get; set; }
    }

}
