using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

using Aikido.Zen.Core.Models;

[assembly: InternalsVisibleTo("Aikido.Zen.Test")]
[assembly: InternalsVisibleTo("Aikido.Zen.Benchmarks")]
[assembly: InternalsVisibleTo("Aikido.Zen.Tests.DotNetCore")]
[assembly: InternalsVisibleTo("Aikido.Zen.Tests.DotNetFramework")]
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

            // since http requests are handled in parallel, we need to lock the cache to prevent race conditions
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

        /// <summary>
        /// Determines if a request should be allowed based on all applicable rate limiting rules
        /// </summary>
        /// <param name="context">The context of the request</param>
        /// <param name="endpoints">The filtered list of endpoints</param>
        /// <returns>A tuple containing: (isAllowed, effectiveConfig) where effectiveConfig is the config that caused rate limiting (if any)</returns>
        public static (bool isAllowed, RateLimitingConfig effectiveConfig) IsRequestAllowed(
            Context context,
            IEnumerable<EndpointConfig> endpoints)
        {
            if (string.IsNullOrEmpty(context?.Method) || string.IsNullOrEmpty(context?.Route))
            {
                return (true, null);
            }

            if (endpoints == null || !endpoints.Any())
            {
                return (true, null);
            }

            // Find endpoints that have rate limiting enabled
            var rateLimitedEndpoints = endpoints
                .Where(e => e.RateLimiting != null && e.RateLimiting.Enabled)
                .ToList();

            if (!rateLimitedEndpoints.Any())
            {
                return (true, null);
            }

            // Get the user ID or IP address for the key
            string userOrIp = context.User?.Id ?? context.RemoteAddress ?? "unknown";
            var config = new RateLimitingConfig();
            // Check exact match first if it exists
            if (RouteHelper.HasExactMatch(context, rateLimitedEndpoints, out var exactMatch))
            {
                config = exactMatch.RateLimiting;
                var exactKey = $"{exactMatch.Method}|{exactMatch.Route}:user-or-ip:{userOrIp}";
                if (!IsAllowed(exactKey, config.WindowSizeInMS, config.MaxRequests))
                {
                    return (false, config);
                }
                return (true, config);
            }

            // Find the best matching endpoints
            var matchingEndpoints = RouteHelper.MatchEndpoints(context, rateLimitedEndpoints);

            // Then check the best matching endpoint
            foreach (var endpoint in matchingEndpoints)
            {

                config = endpoint.RateLimiting;
                if (config != null && config.Enabled)
                {
                    var matchKey = $"{endpoint.Method}|{endpoint.Route}:user-or-ip:{userOrIp}";
                    if (!IsAllowed(matchKey, config.WindowSizeInMS, config.MaxRequests))
                    {
                        return (false, config);
                    }
                }
            }

            // If we get here, all checks passed
            return (true, config);
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
