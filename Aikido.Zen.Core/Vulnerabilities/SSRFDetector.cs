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

        internal static bool HasSameHostAndPort(Uri uri1, Uri uri2)
        {
            if (!EnvironmentHelper.TrustProxy)
            {
                return false;
            }

            var result = Uri.Compare(uri1, uri2, UriComponents.HostAndPort, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase);
            return result == 0;
        }

        internal static bool IsRequestToItself(Uri serverUri, Uri outboundUri)
        {
            if (!EnvironmentHelper.TrustProxy || serverUri == null || outboundUri == null)
            {
                return false;
            }

            var serverHostname = NormalizeHostname(serverUri.Host);
            var outboundHostname = NormalizeHostname(outboundUri.Host);
            if (!string.Equals(serverHostname, outboundHostname, StringComparison.Ordinal))
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
            if (ImdsHelper.IsTrustedHostname(normalizedHostname))
            {
                return false;
            }

            // Stored SSRF only applies to hostname resolution spoofing. If the original target was
            // already a literal IP address, this is not a stored SSRF case.
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

        private static bool HaveEquivalentSelfRequestPorts(int serverPort, int outboundPort)
        {
            if (serverPort == outboundPort)
            {
                return true;
            }

            if (serverPort == 80 && outboundPort == 443)
            {
                return true;
            }

            if (serverPort == 443 && outboundPort == 80)
            {
                return true;
            }

            return false;
        }
    }
}
