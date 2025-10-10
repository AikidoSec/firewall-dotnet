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
        /// Gets the Aikido URL from the environment variables or defaults to a predefined URL.
        /// </summary>
        public static string AikidoUrl => Environment.GetEnvironmentVariable("AIKIDO_URL") ?? "https://guard.aikido.dev";

        /// <summary>
        /// Gets the Aikido real-time URL from the environment variables or defaults to a predefined URL.
        /// </summary>
        public static string AikidoRealtimeUrl => Environment.GetEnvironmentVariable("AIKIDO_REALTIME_URL") ?? "https://runtime.aikido.dev";

        /// <summary>
        /// Determines if the system is in debugging mode by checking the environment variable.
        /// </summary>
        public static bool IsDebugging => GetBooleanValue("AIKIDO_DEBUG");

        /// <summary>
        /// Determines if the system is disabled by checking the environment variable.
        /// </summary>
        public static bool IsDisabled => GetBooleanValue("AIKIDO_DISABLE");

        /// <summary>
        /// Determines whether to trust the X-Forwarded-For header.
        /// Should be set to false if the application is not behind a reverse proxy.
        /// </summary>
        public static bool TrustProxy => GetBooleanValue("AIKIDO_TRUST_PROXY", true);

        /// <summary>
        /// Determines whether to trust the X-Forwarded-For header.
        /// Should be set to false if the application is not behind a reverse proxy.
        /// </summary>
        public static string ClientIpHeader => Environment.GetEnvironmentVariable("AIKIDO_CLIENT_IP_HEADER") ?? "X-FORWARDED-FOR";

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
            if (string.IsNullOrEmpty(AikidoRealtimeUrl))
            {
                LogHelper.InfoLog(Agent.Logger, "Aikido realtime URL not set");
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
