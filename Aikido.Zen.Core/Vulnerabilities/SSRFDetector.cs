using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;

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

        internal static InspectionResult Detect(string hostname, int? port, IPAddress[] addresses, Context context, out bool inspectDns)
        {
            inspectDns = false;

            Uri.TryCreate(context?.Url, UriKind.Absolute, out var serverUri);
            // Allow the app to call itself when the current request URL is trusted.
            if (HasSameHostAndPort(serverUri, hostname, port))
            {
                return InspectionResult.Allow();
            }

            string privateIPAddress;
            if (addresses != null)
            {
                // DNS sink already has resolved addresses, so inspect the real destination IPs.
                if (!TryGetPrivateOrLocalIPAddress(addresses, out privateIPAddress))
                {
                    // Resolved public addresses are not SSRF.
                    return InspectionResult.Allow();
                }
            }
            else if (!TryGetPrivateOrLocalIPAddress(hostname, out privateIPAddress))
            {
                // Outbound sink only has a hostname; inspect DNS later unless it is already a private IP.
                inspectDns = true;
                return InspectionResult.Allow();
            }

            // User-controlled full URLs that resolve to private/local IPs are request SSRF.
            if (!IsRequestToServiceHostname(hostname) && context?.ParsedUserInput != null)
            {
                foreach (var userInput in context.ParsedUserInput)
                {
                    if (!Uri.TryCreate(userInput.Value, UriKind.Absolute, out var userUri) ||
                        !HasSameHostAndPort(userUri, hostname, port))
                    {
                        continue;
                    }

                    var source = UserInputHelper.GetAttackSourceFromUserInputKey(userInput.Key);
                    return InspectionResult.Block(
                        AttackKind.Ssrf,
                        source: source,
                        payload: userInput.Value,
                        metadata: CreateMetadata(hostname, port, privateIPAddress),
                        paths: new[] { UserInputHelper.GetAttackPathFromUserInputKey(userInput.Key) });
                }
            }

            // Unknown-source hostnames resolving to IMDS are stored SSRF.
            if (IsStoredSSRF(hostname, privateIPAddress))
            {
                return InspectionResult.Block(
                    AttackKind.StoredSsrf,
                    metadata: CreateMetadata(hostname, port, privateIPAddress));
            }

            return InspectionResult.Allow();
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

        internal static bool TryGetPrivateOrLocalIPAddress(string candidate, out string privateIPAddress)
        {
            privateIPAddress = null;

            if (string.IsNullOrWhiteSpace(candidate))
            {
                return false;
            }

            var normalizedCandidate = candidate.Trim().TrimStart('[').TrimEnd(']');
            if (!IPAddress.TryParse(normalizedCandidate, out var address))
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

        internal static bool TryGetPrivateOrLocalIPAddress(IPAddress[] addresses, out string privateIPAddress)
        {
            privateIPAddress = addresses
                .Select(address => address?.ToString())
                .FirstOrDefault(address => TryGetPrivateOrLocalIPAddress(address, out _));

            return privateIPAddress != null;
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

        private static IDictionary<string, string> CreateMetadata(string hostname, int? port, string privateIPAddress)
        {
            var metadata = new Dictionary<string, string>
            {
                { "hostname", hostname }
            };

            if (port.HasValue)
            {
                metadata["port"] = port.Value.ToString();
            }

            if (!string.IsNullOrWhiteSpace(privateIPAddress))
            {
                metadata["privateIP"] = privateIPAddress;
            }

            return metadata;
        }

        internal static bool HasSameHostAndPort(Uri uri, string hostname, int? port)
        {
            if (!EnvironmentHelper.TrustProxy || string.IsNullOrWhiteSpace(hostname) || uri == null)
            {
                return false;
            }

            return string.Equals(
                NormalizeHostname(uri.Host),
                NormalizeHostname(hostname),
                StringComparison.Ordinal) &&
                (!port.HasValue || port.Value == uri.Port);
        }
    }
}
