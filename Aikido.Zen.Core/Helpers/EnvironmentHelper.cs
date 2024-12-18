
using System;

namespace Aikido.Zen.Core.Helpers
{
    public static class EnvironmentHelper
    {
        public static string Token => Environment.GetEnvironmentVariable("AIKIDO_TOKEN");
        public static bool DryMode => Environment.GetEnvironmentVariable("AIKIDO_BLOCKING") != "true";
        public static string AikidoUrl => Environment.GetEnvironmentVariable("AIKIDO_URL")
            ?? "https://guard.aikido.dev";
        public static string AikidoRealtimeUrl => Environment.GetEnvironmentVariable("AIKIDO_REALTIME_URL")
            ?? "https://runtime.aikido.dev";
    }
}
