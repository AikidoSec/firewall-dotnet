using System.Diagnostics;
using Aikido.Zen.Core.Models;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

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
        /// Checks if a route matches a wildcard pattern
        /// </summary>
        /// <param name="routePattern">The route pattern that may contain wildcards (*)</param>
        /// <param name="actualRoute">The actual route to check against the pattern</param>
        /// <returns>True if the route matches the pattern, false otherwise</returns>
        public static bool IsWildcardMatch(string routePattern, string actualRoute)
        {
            if (string.IsNullOrEmpty(routePattern)) return false;
            if (string.IsNullOrEmpty(actualRoute)) return false;

            // Convert the wildcard pattern to a regex pattern
            string regexPattern = "^" + Regex.Escape(routePattern).Replace("\\*", ".*") + "$";
            return Regex.IsMatch(actualRoute, regexPattern, RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Determines if a request should be allowed based on all applicable rate limiting rules
        /// </summary>
        /// <param name="routeKey">The route key (method|route)</param>
        /// <param name="userOrIp">The user ID or IP address</param>
        /// <param name="rateLimitedRoutes">Dictionary of all rate limited routes and their configs</param>
        /// <returns>A tuple containing: (isAllowed, effectiveConfig) where effectiveConfig is the config that caused rate limiting (if any)</returns>
        public static (bool isAllowed, RateLimitingConfig effectiveConfig) IsRequestAllowed(
            string routeKey,
            string userOrIp,
            IDictionary<string, RateLimitingConfig> rateLimitedRoutes)
        {
            if (rateLimitedRoutes == null || rateLimitedRoutes.Count == 0)
            {
                return (true, null);
            }

            // Get exact match config if it exists
            rateLimitedRoutes.TryGetValue(routeKey, out var exactConfig);

            // Get all wildcard routes
            var wildcardRoutes = rateLimitedRoutes
                .Where(r => r.Key.Contains("*"))
                .ToDictionary(r => r.Key, r => r.Value);

            // Check exact match first
            if (exactConfig != null && exactConfig.Enabled)
            {
                var exactKey = $"{routeKey}:user-or-ip:{userOrIp}";
                if (!IsAllowed(exactKey, exactConfig.WindowSizeInMS, exactConfig.MaxRequests))
                {
                    return (false, exactConfig);
                }
            }

            // Then check all wildcard matches
            foreach (var wildcardEntry in wildcardRoutes)
            {
                string wildcardRouteKey = wildcardEntry.Key;
                RateLimitingConfig wildcardConfig = wildcardEntry.Value;

                if (wildcardConfig.Enabled && IsWildcardMatch(wildcardRouteKey, routeKey))
                {
                    var wildcardKey = $"{wildcardRouteKey}:user-or-ip:{userOrIp}";
                    if (!IsAllowed(wildcardKey, wildcardConfig.WindowSizeInMS, wildcardConfig.MaxRequests))
                    {
                        return (false, wildcardConfig);
                    }
                }
            }

            // If we get here, all checks passed
            return (true, null);
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
