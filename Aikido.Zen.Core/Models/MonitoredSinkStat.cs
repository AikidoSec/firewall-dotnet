using System.Collections.Generic;

namespace Aikido.Zen.Core.Models
{
    /// <summary>
    /// Statistics for a monitored sink
    /// </summary>
    public class MonitoredSinkStats
    {
        public MonitoredSinkStats()
        {
            AttacksDetected = new AttacksDetected();
            Durations = new List<double>();
            CompressedTimings = new List<CompressedTiming>();
        }

        /// <summary>
        /// Statistics about detected attacks
        /// </summary>
        public AttacksDetected AttacksDetected { get; set; }

        /// <summary>
        /// Number of times the interceptor threw an error
        /// </summary>
        public int InterceptorThrewError { get; set; }

        /// <summary>
        /// Number of calls without context
        /// </summary>
        public int WithoutContext { get; set; }

        /// <summary>
        /// Total number of calls
        /// </summary>
        public int Total { get; set; }

        /// <summary>
        /// List of durations for each sink request in milliseconds
        /// </summary>
        public List<double> Durations { get; set; }

        /// <summary>
        /// List of compressed timing blocks
        /// </summary>
        public List<CompressedTiming> CompressedTimings { get; set; }
    }
}
