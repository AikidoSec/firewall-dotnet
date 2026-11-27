using System.Globalization;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Core.Models
{
    internal readonly struct TrustProxyConfig
    {
        internal bool Enabled { get; }
        internal int HopCount { get; }
        internal IPRange TrustedProxies { get; }

        private TrustProxyConfig(bool enabled, int hopCount = 0, IPRange trustedProxies = null)
        {
            Enabled = enabled;
            HopCount = hopCount;
            TrustedProxies = trustedProxies;
        }

        internal bool IsTrustedProxy(string connectionIp)
        {
            return Enabled && (TrustedProxies == null || TrustedProxies.IsIpInRange(connectionIp));
        }

        internal static TrustProxyConfig Parse(string setting)
        {
            if (int.TryParse(setting?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var hopCount) && hopCount > 0)
            {
                return new TrustProxyConfig(true, hopCount);
            }

            if (setting != null && (setting.Contains(".") || setting.Contains(":")))
            {
                var trustedProxies = new IPRange();
                foreach (var range in setting.Split(','))
                {
                    trustedProxies.InsertRange(range.Trim());
                }

                return new TrustProxyConfig(true, trustedProxies: trustedProxies);
            }

            return new TrustProxyConfig(EnvironmentHelper.GetBoolFromString(setting, acceptNumeric: false, defaultValue: true));
        }
    }
}
