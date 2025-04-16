using System.Collections.Generic;

namespace Aikido.Zen.Core.Models
{
    public class MonitoredSinkStats
    {
        public AttacksDetected AttacksDetected { get; set; }
        public int InterceptorThrewError { get; set; }
        public int WithoutContext { get; set; }
        public int Total { get; set; }
        public IEnumerable<CompressedTiming> CompressedTimings { get; set; }
    }

}
