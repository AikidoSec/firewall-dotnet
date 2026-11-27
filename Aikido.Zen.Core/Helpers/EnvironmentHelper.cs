using System;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// Helper class for accessing environment variables related to the Aikido Zen system.
    /// </summary>
    internal static class EnvironmentHelper
    {
        /// <summary>
        /// Gets the Aikido token from the environment variables.
        /// </summary>
        internal static string Token => Environment.GetEnvironmentVariable("AIKIDO_TOKEN");

        /// <summary>
        /// Determines if the system is in dry mode by checking the environment variable.
        /// </summary>
        internal static bool DryMode => !GetBoolFromString(Environment.GetEnvironmentVariable("AIKIDO_BLOCK"), acceptNumeric: true);

        /// <summary>
        /// Gets the maximum number of API discovery samples from the environment variables.
        /// </summary>
        internal static int MaxApiDiscoverySamples => int.TryParse(Environment.GetEnvironmentVariable("MAX_API_DISCOVERY_SAMPLES"), out int maxHits) ? maxHits : 10;

        /// <summary>
        /// Gets the Aikido URL from the environment variables or derives it from the token's region.
        /// </summary>
        internal static string AikidoUrl => Environment.GetEnvironmentVariable("AIKIDO_ENDPOINT") ?? GetAikidoUrlFromToken(Token);

        /// <summary>
        /// Determines if the system is in debugging mode by checking the environment variable.
        /// </summary>
        internal static bool IsDebugging => GetBoolFromString(Environment.GetEnvironmentVariable("AIKIDO_DEBUG"), acceptNumeric: true);

        /// <summary>
        /// Determines if the system is disabled by checking the environment variable.
        /// </summary>
        internal static bool IsDisabled => GetBoolFromString(Environment.GetEnvironmentVariable("AIKIDO_DISABLE"), acceptNumeric: true);

        /// <summary>
        /// Determines whether to block SQL queries that fail tokenization when user input is present.
        /// Defaults to false (allow by default).
        /// </summary>
        internal static bool BlockInvalidSql => GetBoolFromString(Environment.GetEnvironmentVariable("AIKIDO_BLOCK_INVALID_SQL"), acceptNumeric: true);

        /// <summary>
        /// Determines whether to trust the X-Forwarded-For header.
        /// Should be set to false if the application is not behind a reverse proxy.
        /// </summary>
        internal static string TrustProxy => Environment.GetEnvironmentVariable("AIKIDO_TRUST_PROXY");

        /// <summary>
        /// Determines whether to trust the X-Forwarded-For header.
        /// Should be set to false if the application is not behind a reverse proxy.
        /// </summary>
        internal static string ClientIpHeader => Environment.GetEnvironmentVariable("AIKIDO_CLIENT_IP_HEADER") ?? "X-FORWARDED-FOR";

        /// <summary>
        /// Determines whether to skip the ASP.NET Core endpoint routing startup guard.
        /// Defaults to false so route discovery and endpoint-specific policies use complete routing information.
        /// </summary>
        internal static bool DisableEndpointRoutingCheck => GetBoolFromString(Environment.GetEnvironmentVariable("AIKIDO_DISABLE_ENDPOINT_ROUTING_CHECK"), acceptNumeric: true);

        /// <summary>
        /// Determines whether realtime SSE config notifications are enabled.
        /// The one-minute config poll remains active as a fallback.
        /// </summary>
        internal static bool RealtimeConfigUpdatesEnabled => GetBoolFromString(Environment.GetEnvironmentVariable("AIKIDO_FEATURE_SSE"), acceptNumeric: true);

        /// <summary>
        /// Accepts "true", "yes", "y", and "on" case-insensitively and ignoring surrounding whitespace.
        /// Also accepts "1" when <paramref name="acceptNumeric"/> is enabled.
        /// Uses <paramref name="defaultValue"/> for missing or empty values; all other values return false.
        /// </summary>
        internal static bool GetBoolFromString(string value, bool acceptNumeric, bool defaultValue = false)
        {
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }

            value = value.Trim();

            if (acceptNumeric && value == "1")
            {
                return true;
            }

            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "y", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
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
        internal static string ExtractRegionFromToken(string token)
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

        internal static void ReportValues()
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
