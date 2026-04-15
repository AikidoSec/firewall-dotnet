using System;
using System.Linq;
using System.Net;

namespace Aikido.Zen.Core.Helpers
{
    public class IPHeaderHelper
    {
        /// <summary>
        /// Parses the X-Forwarded-For header or similar headers to extract IP addresses.
        /// Removes any port numbers and normalizes IPv6 addresses by removing brackets.
        /// Does not validate the IP addresses
        /// </summary>
        public static string[] ParseIpHeader(string header)
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

        public static string ParseSingleIp(string ip)
        {
            // According to RFC7239 the X-Forwarded-For header can contain port numbers.
            // If the IP includes a port number, remove it.

            if (IPAddress.TryParse(ip, out var parsedIp))
            {
                // Covers all ipv6 (with/without brackets, with/without port)
                // Covers ipv4 without port
                return parsedIp.ToString();
            }

            // The only supported non-literal form after TryParse is IPv4 with a port.
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
