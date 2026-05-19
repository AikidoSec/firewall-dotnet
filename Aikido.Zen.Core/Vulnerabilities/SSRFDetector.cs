using System;
using System.Collections.Generic;
using System.Globalization;
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

        internal static InspectionResult DetectOutboundRequest(Uri targetUri, Context context, out bool inspectDns)
        {
            inspectDns = false;

            Uri.TryCreate(context?.Url, UriKind.Absolute, out var serverUri);
            if (IsRequestToItself(serverUri, targetUri))
            {
                return InspectionResult.Allow();
            }

            if (TryGetPrivateOrLocalIPAddress(targetUri.Host, out var privateIPAddress))
            {
                return DetectSSRF(targetUri, privateIPAddress, context);
            }

            inspectDns = true;
            return InspectionResult.Allow();
        }

        internal static InspectionResult DetectResolvedRequest(string hostname, string privateIPAddress, Context context)
        {
            if (string.IsNullOrWhiteSpace(privateIPAddress))
            {
                return InspectionResult.Allow();
            }

            if (!IsRequestToServiceHostname(hostname) && context?.ParsedUserInput != null)
            {
                foreach (var userInput in context.ParsedUserInput)
                {
                    if (!Uri.TryCreate(userInput.Value, UriKind.Absolute, out var userUri) ||
                        !HasSameHost(hostname, userUri))
                    {
                        continue;
                    }

                    var source = UserInputHelper.GetAttackSourceFromUserInputKey(userInput.Key);
                    return InspectionResult.Block(
                        AttackKind.Ssrf,
                        source: source,
                        payload: userInput.Value,
                        metadata: CreateMetadata(hostname, null, privateIPAddress),
                        paths: new[] { UserInputHelper.GetAttackPathFromUserInputKey(userInput.Key) });
                }
            }

            if (IsStoredSSRF(hostname, privateIPAddress))
            {
                return InspectionResult.Block(
                    AttackKind.StoredSsrf,
                    metadata: CreateMetadata(hostname, null, privateIPAddress));
            }

            return InspectionResult.Allow();
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

        internal static bool TryGetPrivateOrLocalIPAddress(IPAddress[] addresses, out string privateIPAddress)
        {
            privateIPAddress = null;

            if (addresses == null)
            {
                return false;
            }

            foreach (var address in addresses)
            {
                var candidate = address?.ToString();
                if (!string.IsNullOrWhiteSpace(candidate) && IPHelper.IsPrivateOrLocalIp(candidate))
                {
                    privateIPAddress = candidate;
                    return true;
                }
            }

            return false;
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

        private static InspectionResult DetectSSRF(Uri targetUri, string privateIPAddress, Context context)
        {
            if (string.IsNullOrWhiteSpace(privateIPAddress))
            {
                return InspectionResult.Allow();
            }

            var hostname = targetUri.Host;

            if (!IsRequestToServiceHostname(hostname) && context?.ParsedUserInput != null)
            {
                foreach (var userInput in context.ParsedUserInput)
                {
                    if (!Uri.TryCreate(userInput.Value, UriKind.Absolute, out var userUri) ||
                        !HasSameHostAndPort(targetUri, userUri))
                    {
                        continue;
                    }

                    var source = UserInputHelper.GetAttackSourceFromUserInputKey(userInput.Key);
                    return InspectionResult.Block(
                        AttackKind.Ssrf,
                        source: source,
                        payload: userInput.Value,
                        metadata: CreateMetadata(hostname, UriHelper.GetPort(targetUri), privateIPAddress),
                        paths: new[] { UserInputHelper.GetAttackPathFromUserInputKey(userInput.Key) });
                }
            }

            if (IsStoredSSRF(hostname, privateIPAddress))
            {
                return InspectionResult.Block(
                    AttackKind.StoredSsrf,
                    metadata: CreateMetadata(hostname, null, privateIPAddress));
            }

            return InspectionResult.Allow();
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

        private static bool HasSameHost(string hostname, Uri uri)
        {
            if (!EnvironmentHelper.TrustProxy || string.IsNullOrWhiteSpace(hostname) || uri == null)
            {
                return false;
            }

            return string.Equals(
                NormalizeHostname(hostname),
                NormalizeHostname(uri.Host),
                StringComparison.Ordinal);
        }

        private static bool HaveEquivalentSelfRequestPorts(int serverPort, int outboundPort)
        {
            return serverPort == outboundPort ||
                (serverPort == 80 && outboundPort == 443) ||
                (serverPort == 443 && outboundPort == 80);
        }
    }
}
