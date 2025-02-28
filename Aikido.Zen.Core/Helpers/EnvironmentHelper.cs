using System;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// Helper class for accessing environment variables related to the Aikido Zen system.
    /// </summary>
    public static class EnvironmentHelper
    {
        /// <summary>
        /// Determines if the system is disabled by checking the environment variable.
        /// </summary>
        public static bool IsDisabled => GetBooleanValue("AIKIDO_DISABLE");

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
        /// Helper method to determine if an environment variable is set to "true" or "1".
        /// </summary>
        /// <param name="variableName">The name of the environment variable to check.</param>
        /// <returns>True if the environment variable is set to "true" or "1"; otherwise, false.</returns>
        private static bool GetBooleanValue(string variableName)
        {
            var value = Environment.GetEnvironmentVariable(variableName);
            return value == "true" || value == "1";
        }
    }
}
