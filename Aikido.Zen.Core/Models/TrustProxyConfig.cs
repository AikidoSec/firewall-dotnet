using System.Globalization;
using Aikido.Zen.Core.Helpers;

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

            return new TrustProxyConfig(EnvironmentHelper.GetBoolFromString(setting, acceptNumeric: false, defaultValue: true));
        }
    }
}
