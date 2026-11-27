using System;
using System.Linq;
using System.Net;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core.Helpers
{
    internal class IPHeaderHelper
    {
        internal static string GetClientIpFromHeader(string header, TrustProxyConfig trustProxyConfig)
        {
            var hopCount = trustProxyConfig.HopCount;
            if (hopCount > 0)
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
