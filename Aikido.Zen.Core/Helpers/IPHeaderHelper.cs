using System;
using System.Globalization;
using System.Linq;
using System.Net;

namespace Aikido.Zen.Core.Helpers
{
    internal class IPHeaderHelper
    {
        internal static bool ShouldTrustProxy(string setting)
        {
            return string.IsNullOrEmpty(setting) ||
                string.Equals(setting, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(setting, "yes", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(setting, "y", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(setting, "on", StringComparison.OrdinalIgnoreCase) ||
                TryGetHopCount(setting, out _);
        }

        internal static string GetClientIpFromHeader(string header, string trustProxy = null)
        {
            if (TryGetHopCount(trustProxy, out var hopCount))
            {
                var entries = header?.Split(',');
                if (entries == null || hopCount > entries.Length)
                {
                    return null;
                }

                var clientIp = ParseSingleIp(entries[entries.Length - hopCount].Trim());
                return IPHelper.IsValidIp(clientIp) ? clientIp : null;
            }

            foreach (var ip in ParseIpHeader(header).Reverse())
            {
                if (IPHelper.IsValidIp(ip) && !IPHelper.IsPrivateOrLocalIp(ip))
                {
                    return ip;
                }
            }

            return null;
        }

        private static bool TryGetHopCount(string setting, out int hopCount)
        {
            return int.TryParse(setting, NumberStyles.None, CultureInfo.InvariantCulture, out hopCount) && hopCount > 0;
        }

        /// <summary>
        /// Parses the X-Forwarded-For header or similar headers to extract IP addresses.
        /// Removes any port numbers and normalizes IPv6 addresses by removing brackets.
        /// Does not validate the IP addresses
        /// </summary>
        internal static string[] ParseIpHeader(string header)
        {
            if (string.IsNullOrWhiteSpace(header))
            {
                return Array.Empty<string>();
            }

            return header
                .Split(',')
                .Select(ip => ip.Trim())
                .Where(ip => !string.IsNullOrEmpty(ip))
                .Select(ip => ParseSingleIp(ip))
                .ToArray();
        }

        internal static string ParseSingleIp(string ip)
        {
            // According to RFC7239 the X-Forwarded-For header can contain port numbers
            // We need to return the IP without any port

            if (IPAddress.TryParse(ip, out var parsedIp))
            {
                // Covers ipv4 without port
                // Covers all ipv6 (with/without brackets, with/without port, ipv4-mapped ipv6)
                return parsedIp.ToString();
            }

            // The only supported IP form not extracted by TryParse is IPv4 with a port
            if (ip.Contains('.'))
            {
                var lastColon = ip.LastIndexOf(':');
                if (lastColon > 0)
                {
                    var withoutPort = ip.Substring(0, lastColon);
                    if (IPAddress.TryParse(withoutPort, out parsedIp))
                    {
                        return parsedIp.ToString();
                    }
                }
            }

            return ip;
        }
    }
}
