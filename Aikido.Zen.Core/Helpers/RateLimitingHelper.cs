using System.Diagnostics;
using Aikido.Zen.Core.Models;
using System.Runtime.CompilerServices;
using System.Collections.Generic;

[assembly: InternalsVisibleTo("Aikido.Zen.Test")]
[assembly: InternalsVisibleTo("Aikido.Zen.Benchmarks")]
namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// Helper class for implementing rate limiting functionality
    /// </summary>
    public static class RateLimitingHelper
    {
        private static LRUCache<string, List<long>> RateLimitedItems = new LRUCache<string, List<long>>(10000, 120 * 60 * 1000); // 10000 items, 120 minutes TTL
        private static readonly object _lock = new object();

        /// <summary>
        /// Determines if a request should be allowed based on sliding window rate limiting rules
        /// </summary>
        /// <param name="key">Unique identifier for the rate limit (typically combines route and user/IP)</param>
        /// <param name="windowSizeInMS">Time window in milliseconds for rate limiting</param>
        /// <param name="maxRequests">Maximum number of requests allowed within the time window</param>
        /// <returns>True if request is allowed, false if it should be rate limited</returns>
        public static bool IsAllowed(string key, int windowSizeInMS, int maxRequests)
        {
            if (maxRequests <= 0) return false;
            if (windowSizeInMS <= 0) return true;

            var currentTime = GetCurrentTimestamp();

            lock (_lock)
            {
                // Get or create the list of timestamps for this key
                if (!RateLimitedItems.TryGetValue(key, out var timestamps))
                {
                    timestamps = new List<long> { currentTime };
                    RateLimitedItems.Set(key, timestamps);
                    return true;
                }

                // Remove timestamps that are outside the window
                timestamps.RemoveAll(timestamp => currentTime - timestamp > windowSizeInMS);

                // Add current timestamp
                timestamps.Add(currentTime);

                // Update the cache with filtered timestamps
                RateLimitedItems.Set(key, timestamps);

                // Check if the number of requests is within limits
                return timestamps.Count <= maxRequests;
            }
        }

        private static long GetCurrentTimestamp()
        {
            return (long)(Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency * 1000);
        }

        internal static void ResetCache(int size, int ttlInMs)
        {
            RateLimitedItems = new LRUCache<string, List<long>>(size, ttlInMs);
        }
    }
}
