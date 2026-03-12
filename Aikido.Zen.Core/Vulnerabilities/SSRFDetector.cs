using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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
        private static readonly HashSet<IPAddress> ImdsIPAddresses = new HashSet<IPAddress>
        {
            IPAddress.Parse("169.254.169.254"),
            IPAddress.Parse("::ffff:169.254.169.254"),
            IPAddress.Parse("100.100.100.200"),
            IPAddress.Parse("::ffff:100.100.100.200"),
            IPAddress.Parse("fd00:ec2::254")
        };

        internal static bool IsSuspiciousTarget(Uri targetUri, Uri serverUri, out string privateIPAddress)
        {
            privateIPAddress = null;

            if (targetUri == null || serverUri == null)
            {
                return false;
            }

            var targetHost = targetUri.Host;
            var normalizedTargetHost = NormalizeHostname(targetHost);

            // Skip internal service names and request-to-self cases before doing any IP checks.
            if (IsRequestToServiceHostname(targetHost) || HasSameHostAndPort(targetUri, serverUri))
            {
                return false;
            }

            // Direct private IPs are suspicious immediately and don't need DNS resolution.
            if (ContainsPrivateIPAddress(normalizedTargetHost))
            {
                return true;
            }

            // Target hostname is suspicious when it resolves to a private or local IP.
            try
            {
                var resolvedPrivateIPAddress = Dns
                    .GetHostAddresses(normalizedTargetHost)
                    .FirstOrDefault(address => IPHelper.IsPrivateOrLocalIp(address.ToString()));
                if (resolvedPrivateIPAddress == null)
                {
                    return false;
                }

                privateIPAddress = resolvedPrivateIPAddress.ToString();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

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

            if (TrustedImdsHostnames.Contains(NormalizeHostname(hostname)))
            {
                return false;
            }

            return IPAddress.TryParse(privateIPAddress, out var parsedPrivateIPAddress) &&
                ImdsIPAddresses.Contains(parsedPrivateIPAddress);
        }

        private static bool ContainsPrivateIPAddress(string hostname)
        {
            var ip = TrimIPv6Brackets(hostname);
            return IPAddress.TryParse(ip, out var parsedAddress) && IPHelper.IsPrivateOrLocalIp(parsedAddress.ToString());
        }

        private static string NormalizeHostname(string hostname)
        {
            if (string.IsNullOrWhiteSpace(hostname))
            {
                return hostname;
            }

            var normalizedHostname = TrimIPv6Brackets(hostname).Trim().TrimEnd('.').ToLowerInvariant();
            try
            {
                return HostnameIdnMapping.GetAscii(normalizedHostname).ToLowerInvariant();
            }
            catch (ArgumentException)
            {
                return normalizedHostname;
            }
        }

        private static string TrimIPv6Brackets(string hostname)
        {
            if (string.IsNullOrWhiteSpace(hostname))
            {
                return hostname;
            }

            return hostname.Trim().TrimStart('[').TrimEnd(']');
        }
    }
}
