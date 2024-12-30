using System.Diagnostics;
using Aikido.Zen.Core.Models;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Aikido.Zen.Test")]
[assembly: InternalsVisibleTo("Aikido.Zen.Benchmarks")]
namespace Aikido.Zen.Core.Helpers
{
    public static class RateLimitingHelper
    {
        private static LRUCache<string, RequestInfo> RateLimitedItems = new LRUCache<string, RequestInfo>(10000, 120 * 60 * 1000); // 10000 items, 120 minutes TTL

        public static bool IsAllowed(string key, int windowSizeInMS, int maxRequests)
        {
            if (maxRequests <= 0) return false;
            if (windowSizeInMS <= 0) return true;

            var currentTime = GetCurrentTimestamp();
            RequestInfo requestInfo;

            if (!RateLimitedItems.TryGetValue(key, out requestInfo))
            {
                RateLimitedItems.Set(key, new RequestInfo(1, currentTime));
                return true;
            }

            var elapsedTime = currentTime - requestInfo.StartTime;

            if (elapsedTime >= windowSizeInMS)
            {
                // Reset the counter and timestamp if windowSizeInMS has expired
                RateLimitedItems.Set(key, new RequestInfo(1, currentTime));
                return true;
            }

            if (requestInfo.Count < maxRequests)
            {
                // Increment the counter if it is within the windowSizeInMS and maxRequests
                requestInfo.Count++;
                RateLimitedItems.Set(key, requestInfo); // Update the value in cache
                return true;
            }

            // Deny the request if the maxRequests is reached within windowSizeInMS
            return false;
        }

        private static long GetCurrentTimestamp()
        {
            return (long)(Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency * 1000);
        }

        internal static void ResetCache(int size, int ttlInMs)
        {
            RateLimitedItems = new LRUCache<string, RequestInfo>(size, ttlInMs);
        }

        internal struct RequestInfo {
            public int Count;
            public long StartTime;

            internal RequestInfo(int count, long startTime)
            {
                Count = count;
                StartTime = startTime;
            }
        }
    }
}
