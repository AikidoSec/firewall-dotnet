
using System;

namespace Aikido.Zen.Core.Helpers
{
    public static class EnvironmentHelper
    {
        public static string Token => Environment.GetEnvironmentVariable("AIKIDO_TOKEN");
        public static bool DryMode => Environment.GetEnvironmentVariable("AIKIDO_BLOCKING") != "true";
    }
}
