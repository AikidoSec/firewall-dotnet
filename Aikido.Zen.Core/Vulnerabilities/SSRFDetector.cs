using System;
using System.Collections.Generic;
using System.Globalization;
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

        internal static bool HasSameHostAndPort(Uri left, Uri right)
        {
            if (!EnvironmentHelper.TrustProxy || left == null || right == null)
            {
                return false;
            }

            return Uri.Compare(
                left,
                right,
                UriComponents.HostAndPort,
                UriFormat.SafeUnescaped,
                StringComparison.OrdinalIgnoreCase) == 0;
        }

        internal static bool IsRequestToItself(Uri serverUri, Uri outboundUri)
        {
            if (!EnvironmentHelper.TrustProxy || serverUri == null || outboundUri == null)
            {
                return false;
            }

            if (!string.Equals(
                    NormalizeHostname(serverUri.Host),
                    NormalizeHostname(outboundUri.Host),
                    StringComparison.Ordinal))
            {
                return false;
            }

            return HaveEquivalentSelfRequestPorts(serverUri.Port, outboundUri.Port);
        }

        internal static bool IsRequestToServiceHostname(string hostname)
        {
            if (string.IsNullOrWhiteSpace(hostname))
            {
                return false;
            }

            var normalizedHostname = NormalizeHostname(hostname);
            return !NotServiceHostnames.Contains(normalizedHostname) &&
                ServiceHostnamePattern.IsMatch(normalizedHostname);
        }

        internal static bool IsStoredSSRF(string hostname, string privateIPAddress)
        {
            if (string.IsNullOrWhiteSpace(hostname) || string.IsNullOrWhiteSpace(privateIPAddress))
            {
                return false;
            }

            var normalizedHostname = NormalizeHostname(hostname);
            if (ImdsHelper.IsTrustedHostname(normalizedHostname))
            {
                return false;
            }

            if (IPAddress.TryParse(normalizedHostname, out _))
            {
                return false;
            }

            return ImdsHelper.IsImdsIPAddress(privateIPAddress);
        }

        internal static bool TryGetPrivateOrLocalIPAddress(string hostname, out string privateIPAddress)
        {
            privateIPAddress = null;

            if (string.IsNullOrWhiteSpace(hostname))
            {
                return false;
            }

            var candidate = hostname.Trim().TrimStart('[').TrimEnd(']');
            if (!IPAddress.TryParse(candidate, out var address))
            {
                return false;
            }

            var normalizedIPAddress = address.ToString();
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

        private static bool HaveEquivalentSelfRequestPorts(int serverPort, int outboundPort)
        {
            return serverPort == outboundPort ||
                (serverPort == 80 && outboundPort == 443) ||
                (serverPort == 443 && outboundPort == 80);
        }
    }
}
