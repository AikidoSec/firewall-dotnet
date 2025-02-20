using System.Collections.Generic;

namespace Aikido.Zen.Core.Models
{
    /// <summary>
    /// Represents compressed performance statistics for a monitored sink
    /// </summary>
    public class CompressedTiming
    {
        /// <summary>
        /// Average duration in milliseconds
        /// </summary>
        public double AverageInMS { get; set; }

        /// <summary>
        /// Performance percentiles (50th, 75th, 90th, 95th, 99th)
        /// </summary>
        public Dictionary<string, double> Percentiles { get; set; } = new Dictionary<string, double>();

        /// <summary>
        /// Unix timestamp in milliseconds when the stats were compressed
        /// </summary>
        public long CompressedAt { get; set; }
    }
}
