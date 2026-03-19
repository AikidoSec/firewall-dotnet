using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Core.Vulnerabilities
{
    internal static class SSRFDetector
    {
        private static readonly IdnMapping HostnameIdnMapping = new IdnMapping();
        private static readonly Regex ServiceHostnamePattern = new Regex("^[a-z-_]+$", RegexOptions.Compiled);
        private static readonly HashSet<string> NotServiceHostnames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "localhost",
            "localdomain",
            "metadata"
        };
        private static readonly HashSet<string> TrustedImdsHostnames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "metadata.google.internal",
            "metadata.goog"
        };
        private static readonly HashSet<string> ImdsIPAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "169.254.169.254",
            "::ffff:169.254.169.254",
            "100.100.100.200",
            "::ffff:100.100.100.200",
            "fd00:ec2::254"
        };

        internal static bool HasSameHostAndPort(Uri uri1, Uri uri2)
        {
            if (!EnvironmentHelper.TrustProxy)
            {
                return false;
            }

            var result = Uri.Compare(uri1, uri2, UriComponents.HostAndPort, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase);
            return result == 0;
        }

        internal static bool IsRequestToServiceHostname(string hostname)
        {
            if (string.IsNullOrWhiteSpace(hostname))
            {
                return false;
            }

            var normalizedHostname = NormalizeHostname(hostname);
            if (NotServiceHostnames.Contains(normalizedHostname))
            {
                return false;
            }

            return ServiceHostnamePattern.IsMatch(normalizedHostname);
        }

        internal static bool IsStoredSSRF(string hostname, string privateIPAddress)
        {
            if (string.IsNullOrWhiteSpace(hostname) || string.IsNullOrWhiteSpace(privateIPAddress))
            {
                return false;
            }

            var normalizedHostname = NormalizeHostname(hostname);
            // These are legitimate metadata service hostnames, so reaching an IMDS IP through them
            // is not evidence of hostname spoofing.
            if (TrustedImdsHostnames.Contains(normalizedHostname))
            {
                return false;
            }

            // Stored SSRF only applies to hostname resolution spoofing. If the original target was
            // already a literal IP address, this is not a stored SSRF case.
            if (IPAddress.TryParse(normalizedHostname, out _))
            {
                return false;
            }

            return ImdsIPAddresses.Contains(privateIPAddress);
        }

        internal static bool TryGetPrivateOrLocalIPAddress(string hostname, out string privateIPAddress)
        {
            privateIPAddress = null;

            if (string.IsNullOrWhiteSpace(hostname))
            {
                return false;
            }

            var normalizedCandidate = hostname.Trim().TrimStart('[').TrimEnd(']');
            if (!IPAddress.TryParse(normalizedCandidate, out var parsedAddress))
            {
                return false;
            }

            var normalizedIPAddress = parsedAddress.ToString();
            if (!IPHelper.IsPrivateOrLocalIp(normalizedIPAddress))
            {
                return false;
            }

            privateIPAddress = normalizedIPAddress;
            return true;
        }

        internal static string NormalizeHostname(string hostname)
        {
            if (string.IsNullOrWhiteSpace(hostname))
            {
                return hostname;
            }

            var normalizedHostname = hostname.Trim().TrimStart('[').TrimEnd(']').ToLowerInvariant();
            try
            {
                return HostnameIdnMapping.GetAscii(normalizedHostname).ToLowerInvariant();
            }
            catch (ArgumentException)
            {
                return normalizedHostname;
            }
        }
    }
}
