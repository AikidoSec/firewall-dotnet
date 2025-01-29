
using System;

namespace Aikido.Zen.Core.Helpers
{
    public static class EnvironmentHelper
    {
        public static string Token => Environment.GetEnvironmentVariable("AIKIDO_TOKEN");
        public static bool DryMode => GetDryMode();

        public static int MaxApiDiscoverySamples => int.TryParse(Environment.GetEnvironmentVariable("MAX_API_DISCOVERY_SAMPLES"), out int maxHits) ? maxHits : 10;

        private static bool GetDryMode()
        {
            var blocking = Environment.GetEnvironmentVariable("AIKIDO_BLOCK");
            if (blocking == "true" || blocking == "1")
                return false;
            return true;
        }

        public static string AikidoUrl => Environment.GetEnvironmentVariable("AIKIDO_URL")
            ?? "https://guard.aikido.dev";
        public static string AikidoRealtimeUrl => Environment.GetEnvironmentVariable("AIKIDO_REALTIME_URL")
            ?? "https://runtime.aikido.dev";
    }
}
