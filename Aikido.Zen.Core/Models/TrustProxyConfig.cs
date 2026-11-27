using System;
using System.Globalization;

namespace Aikido.Zen.Core.Models
{
    internal readonly struct TrustProxyConfig
    {
        internal bool Enabled { get; }
        internal int HopCount { get; }

        private TrustProxyConfig(bool enabled, int hopCount = 0)
        {
            Enabled = enabled;
            HopCount = hopCount;
        }

        internal static TrustProxyConfig Parse(string setting)
        {
            if (int.TryParse(setting, NumberStyles.None, CultureInfo.InvariantCulture, out var hopCount) && hopCount > 0)
            {
                return new TrustProxyConfig(true, hopCount);
            }

            var enabled = string.IsNullOrEmpty(setting) ||
                string.Equals(setting, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(setting, "yes", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(setting, "y", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(setting, "on", StringComparison.OrdinalIgnoreCase);

            return new TrustProxyConfig(enabled);
        }
    }
}
