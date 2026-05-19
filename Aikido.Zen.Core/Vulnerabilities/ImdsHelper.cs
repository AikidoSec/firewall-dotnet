using System;
using System.Collections.Generic;
using System.Net;

namespace Aikido.Zen.Core.Vulnerabilities
{
    internal static class ImdsHelper
    {
        private static readonly HashSet<string> ImdsIPAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "169.254.169.254",
            "100.100.100.200",
            "fd00:ec2::254"
        };

        private static readonly HashSet<string> TrustedHostnames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "metadata.google.internal",
            "metadata.goog"
        };

        internal static bool IsImdsIPAddress(string ipAddress)
        {
            var normalizedIPAddress = NormalizeIPAddress(ipAddress);
            return normalizedIPAddress != null && ImdsIPAddresses.Contains(normalizedIPAddress);
        }

        internal static bool IsTrustedHostname(string hostname)
        {
            return !string.IsNullOrWhiteSpace(hostname) &&
                TrustedHostnames.Contains(hostname.Trim());
        }

        private static string NormalizeIPAddress(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                return null;
            }

            var normalizedCandidate = ipAddress.Trim().TrimStart('[').TrimEnd(']');
            if (!IPAddress.TryParse(normalizedCandidate, out var parsedAddress))
            {
                return null;
            }

            if (parsedAddress.IsIPv4MappedToIPv6)
            {
                return parsedAddress.MapToIPv4().ToString();
            }

            return parsedAddress.ToString();
        }
    }
}
