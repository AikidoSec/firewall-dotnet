using System;
using System.Diagnostics;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// Extension methods for recording sink statistics in the Aikido Zen system.
    /// </summary>
    public static class StatHelper
    {
        /// <summary>
        /// Records statistics for an inspected sink call.
        /// </summary>
        /// <param name="stats">The Stats instance to record to.</param>
        /// <param name="sink">The name of the sink being monitored.</param>
        /// <param name="attackDetected">Whether an attack was detected.</param>
        /// <param name="blocked">Whether the attack was blocked.</param>
        /// <param name="durationInMs">The duration of the sink call in milliseconds.</param>
        /// <param name="withoutContext">Whether the sink was called without context.</param>
        public static void OnInspectedCall(this Stats stats, string sink, bool attackDetected, bool blocked, double durationInMs, bool withoutContext)
        {
            if (stats == null)
                throw new ArgumentNullException(nameof(stats));
            if (string.IsNullOrEmpty(sink))
                throw new ArgumentNullException(nameof(sink));

            stats.AddSinkStat(sink, attackDetected, blocked, durationInMs, withoutContext);
        }

        /// <summary>
        /// Records statistics for an inspected sink call using a Stopwatch for timing.
        /// </summary>
        /// <param name="stats">The Stats instance to record to.</param>
        /// <param name="sink">The name of the sink being monitored.</param>
        /// <param name="attackDetected">Whether an attack was detected.</param>
        /// <param name="blocked">Whether the attack was blocked.</param>
        /// <param name="stopwatch">The Stopwatch used to measure the duration.</param>
        /// <param name="withoutContext">Whether the sink was called without context.</param>
        public static void OnInspectedCall(this Stats stats, string sink, bool attackDetected, bool blocked, Stopwatch stopwatch, bool withoutContext)
        {
            if (stats == null)
                throw new ArgumentNullException(nameof(stats));
            if (string.IsNullOrEmpty(sink))
                throw new ArgumentNullException(nameof(sink));
            if (stopwatch == null)
                throw new ArgumentNullException(nameof(stopwatch));

            var durationInMs = stopwatch.Elapsed.TotalMilliseconds;
            stats.AddSinkStat(sink, attackDetected, blocked, durationInMs, withoutContext);
        }

        /// <summary>
        /// Starts a new Stopwatch.
        /// </summary>
        /// <returns>A new Stopwatch instance.</returns>
        public static Stopwatch StartTimer()
        {
            return Stopwatch.StartNew();
        }

        /// <summary>
        /// Stops the Stopwatch and returns the elapsed time in milliseconds.
        /// </summary>
        /// <param name="stopwatch">The Stopwatch to stop.</param>
        /// <returns>The elapsed time in milliseconds.</returns>
        public static double StopTimer(this Stopwatch stopwatch)
        {
            stopwatch.Stop();
            return stopwatch.Elapsed.TotalMilliseconds;
        }

    }
}
