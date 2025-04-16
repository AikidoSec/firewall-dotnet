using System.Collections.Generic;

namespace Aikido.Zen.Core.Models
{
    public class CompressedTiming
    {
        public double AverageInMS { get; set; }
        public Dictionary<string, double> Percentiles { get; set; } = new Dictionary<string, double>();
        public int CompressedAt { get; set; }
    }
}
