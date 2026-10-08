using System;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// Helper class for accessing environment variables related to the Aikido Zen system.
    /// </summary>
    public static class EnvironmentHelper
    {
        /// <summary>
        /// Gets the Aikido token from the environment variables.
        /// </summary>
        public static string Token => Environment.GetEnvironmentVariable("AIKIDO_TOKEN");

        /// <summary>
        /// Determines if the system is in dry mode by checking the environment variable.
        /// </summary>
        public static bool DryMode => !GetBooleanValue("AIKIDO_BLOCK");

        /// <summary>
        /// Gets the maximum number of API discovery samples from the environment variables.
        /// </summary>
        public static int MaxApiDiscoverySamples => int.TryParse(Environment.GetEnvironmentVariable("MAX_API_DISCOVERY_SAMPLES"), out int maxHits) ? maxHits : 10;

        /// <summary>
        /// Gets the Aikido URL from the environment variables or derives it from the token's region.
        /// </summary>
        public static string AikidoUrl => Environment.GetEnvironmentVariable("AIKIDO_ENDPOINT") ?? GetAikidoUrlFromToken(Token);

        /// <summary>
        /// Determines if the system is in debugging mode by checking the environment variable.
        /// </summary>
        public static bool IsDebugging => GetBooleanValue("AIKIDO_DEBUG");

        /// <summary>
        /// Determines if the system is disabled by checking the environment variable.
        /// </summary>
        public static bool IsDisabled => GetBooleanValue("AIKIDO_DISABLE");

        /// <summary>
        /// Determines whether to block SQL queries that fail tokenization when user input is present.
        /// Defaults to false (allow by default).
        /// </summary>
        public static bool BlockInvalidSql => GetBooleanValue("AIKIDO_BLOCK_INVALID_SQL");

        /// <summary>
        /// Determines whether to trust the X-Forwarded-For header.
        /// Should be set to true only if the application is behind a trusted reverse proxy.
        /// Defaults to false for security (prevents IP spoofing).
        /// </summary>
        public static bool TrustProxy => GetBooleanValue("AIKIDO_TRUST_PROXY", false);

        /// <summary>
        /// Gets the header name to use for client IP extraction when behind a proxy.
        /// Defaults to X-FORWARDED-FOR.
        /// </summary>
        public static string ClientIpHeader => Environment.GetEnvironmentVariable("AIKIDO_CLIENT_IP_HEADER") ?? "X-FORWARDED-FOR";

        /// <summary>
        /// Gets the comma-separated list of trusted proxy IP addresses or CIDR ranges.
        /// Only requests from these proxies will have their forwarding headers trusted.
        /// If not set and TrustProxy is true, all proxies are trusted (insecure).
        /// </summary>
        public static string TrustedProxies => Environment.GetEnvironmentVariable("AIKIDO_TRUSTED_PROXIES");

        private static Aikido.Zen.Core.Models.Ip.IPRange _trustedProxyRange = null;
        private static bool _trustedProxyRangeInitialized = false;
        private static readonly object _trustedProxyLock = new object();

        /// <summary>
        /// Resets the trusted proxy cache. Used for testing purposes.
        /// </summary>
        internal static void ResetTrustedProxyCache()
        {
            lock (_trustedProxyLock)
            {
                _trustedProxyRange = null;
                _trustedProxyRangeInitialized = false;
            }
        }

        /// <summary>
        /// Checks if a given IP address is in the trusted proxy list.
        /// Returns true if no trusted proxies are configured (legacy behavior for backward compatibility).
        /// </summary>
        /// <param name="ip">The IP address to check.</param>
        /// <returns>True if the IP is a trusted proxy or no proxies are configured, false otherwise.</returns>
        public static bool IsTrustedProxy(string ip)
        {
            if (!_trustedProxyRangeInitialized)
            {
                lock (_trustedProxyLock)
                {
                    if (!_trustedProxyRangeInitialized)
                    {
                        var trustedProxies = TrustedProxies;
                        if (!string.IsNullOrWhiteSpace(trustedProxies))
                        {
                            _trustedProxyRange = new Aikido.Zen.Core.Models.Ip.IPRange();
                            var proxies = trustedProxies.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var proxy in proxies)
                            {
                                var trimmedProxy = proxy.Trim();
                                if (!string.IsNullOrWhiteSpace(trimmedProxy))
                                {
                                    foreach (var cidr in IPHelper.ToCidrString(trimmedProxy))
                                    {
                                        _trustedProxyRange.InsertRange(cidr);
                                    }
                                }
                            }
                        }
                        _trustedProxyRangeInitialized = true;
                    }
                }
            }

            // If no trusted proxies configured, trust all (legacy behavior)
            if (_trustedProxyRange == null || !_trustedProxyRange.HasItems)
            {
                return true;
            }

            // Validate the IP is in the trusted proxy range
            if (!IPHelper.IsValidIp(ip))
            {
                return false;
            }

            return _trustedProxyRange.IsIpInRange(ip);
        }

        /// <summary>
        /// Determines whether to skip the ASP.NET Core endpoint routing startup guard.
        /// Defaults to false so route discovery and endpoint-specific policies use complete routing information.
        /// </summary>
        public static bool DisableEndpointRoutingCheck => GetBooleanValue("AIKIDO_DISABLE_ENDPOINT_ROUTING_CHECK");

        /// <summary>
        /// Determines whether realtime SSE config notifications are enabled.
        /// The one-minute config poll remains active as a fallback.
        /// </summary>
        public static bool RealtimeConfigUpdatesEnabled => GetBooleanValue("AIKIDO_FEATURE_SSE");

        /// <summary>
        /// Helper method to determine if an environment variable is set to "true" or "1".
        /// </summary>
        /// <param name="variableName">The name of the environment variable to check.</param>
        /// <returns>True if the environment variable is set to "true" or "1"; otherwise, false.</returns>
        private static bool GetBooleanValue(string variableName, bool defaultValue = false)
        {
            var value = Environment.GetEnvironmentVariable(variableName);
            if (value == null)
            {
                return defaultValue;
            }
            return value == "true" || value == "1";
        }

        /// <summary>
        /// Gets the Aikido URL derived from the region encoded in the given token.
        /// </summary>
        private static string GetAikidoUrlFromToken(string token)
        {
            switch (ExtractRegionFromToken(token))
            {
                case "US":
                    return "https://guard.us.aikido.dev";
                case "ME":
                    return "https://guard.me.aikido.dev";
                case "AU":
                    return "https://guard.au.aikido.dev";
                default:
                    return "https://guard.aikido.dev";
            }
        }

        /// <summary>
        /// Extracts the region from an Aikido runtime token.
        /// </summary>
        /// <param name="token">The Aikido runtime token, e.g. AIK_RUNTIME_{sys_group_id}_{service_id}_{region}_{random}.</param>
        /// <returns>The region code (e.g. "EU", "US", "ME", "AU"). Defaults to "EU" if the token does not contain a region.</returns>
        public static string ExtractRegionFromToken(string token)
        {
            if (string.IsNullOrEmpty(token) || !token.StartsWith("AIK_RUNTIME_"))
            {
                return "EU";
            }

            var tokenWithoutPrefix = token.Substring("AIK_RUNTIME_".Length);
            var parts = tokenWithoutPrefix.Split('_');

            // New format: AIK_RUNTIME_{sys_group_id}_{service_id}_{region}_{random}
            // Old format: AIK_RUNTIME_{sys_group_id}_{service_id}_{random}
            if (parts.Length == 4)
            {
                return parts[2];
            }

            return "EU";
        }

        public static void ReportValues()
        {
            if (string.IsNullOrEmpty(Token))
            {
                LogHelper.InfoLog(Agent.Logger, "Aikido token not set, Zen will not report any events and receive no configuration updates.");
            }
            if (string.IsNullOrEmpty(AikidoUrl))
            {
                LogHelper.InfoLog(Agent.Logger, "Aikido URL not set, Zen will not report any events and receive no configuration updates.");
            }
            if (DryMode)
            {
                LogHelper.InfoLog(Agent.Logger, "Zen is running in dry mode. Attacks are not blocked, but firewall rules are applied.");
            }
            else
            {
                LogHelper.InfoLog(Agent.Logger, "Zen is running in blocking mode. Attacks are blocked.");
            }
            if (IsDisabled)
            {
                LogHelper.InfoLog(Agent.Logger, "Zen is disabled.");
            }
        }
    }
}
