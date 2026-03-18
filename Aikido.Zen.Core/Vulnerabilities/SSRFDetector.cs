using System;
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

        internal static bool IsSuspiciousTarget(Uri targetUri, string serverUrl, out string privateIPAddress)
        {
            privateIPAddress = null;

            var hostname = targetUri.Host;
            var port = targetUri.Port;

            if (string.IsNullOrWhiteSpace(hostname))
            {
                return false;
            }

            // Skip internal service names and request-to-self cases before doing any IP checks.
            if (IsRequestToServiceHostname(hostname) || IsRequestToItself(targetUri, serverUrl, port))
            {
                return false;
            }

            // Direct private IPs are suspicious immediately and don't need DNS resolution.
            if (ContainsPrivateIPAddress(hostname))
            {
                return true;
            }

            // Hostnames are suspicious when they resolve to a private or local IP.
            try
            {
                var resolvedPrivateIPAddress = Dns
                    .GetHostAddresses(hostname)
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

        internal static bool FindTargetInUserInput(Uri targetUri, string userInput)
        {
            var targetHost = targetUri.Host;
            var targetPort = targetUri.Port;

            if (string.IsNullOrWhiteSpace(userInput) || userInput.Length <= 1 || string.IsNullOrWhiteSpace(targetHost))
            {
                return false;
            }

            // foreach (var candidate in GetUserInputCandidates(userInput))
            // {
            var userInputUri = TryCreateAbsoluteUri(userInput);
            if (userInputUri == null || !string.Equals(userInputUri.Host, targetHost, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (userInputUri.Port == targetPort.Value)
            {
                return true;
            }
            // }

            return false;
        }

        internal static bool IsRequestToItself(Uri targetUri, Uri serverUri)
        {
            if (!EnvironmentHelper.TrustProxy)
            {
                return false;
            }

            if (serverUri == null || outboundUri == null || !string.Equals(serverUri.Host, targetUri.Host, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var targetPort = targetUri.Port;
            var serverPort = serverUri.Port;

            if (targetPort == serverPort)
            {
                return true;
            }

            return (targetPort == 80 && serverPort == 443) || (targetPort == 443 && serverPort == 80);
        }

        internal static bool IsRequestToServiceHostname(string hostname)
        {
            if (string.IsNullOrWhiteSpace(hostname))
            {
                return false;
            }

            var lowerHostname = hostname.ToLowerInvariant();
            if (NotServiceHostnames.Contains(lowerHostname))
            {
                return false;
            }

            return ServiceHostnamePattern.IsMatch(lowerHostname);
        }

        internal static bool IsStoredSSRF(string hostname, string privateIPAddress)
        {
            if (string.IsNullOrWhiteSpace(hostname) || string.IsNullOrWhiteSpace(privateIPAddress))
            {
                return false;
            }

            if (TrustedImdsHostnames.Contains(hostname))
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

        private static Uri TryCreateAbsoluteUri(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
            {
                return uri;
            }

            return null;
        }

        // private static IEnumerable<string> GetUserInputCandidates(string userInput)
        // {
        //     var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        //     var candidates = new List<string>();
        //     var trimmedInput = userInput.Trim();

        //     AddCandidate(candidates, seen, trimmedInput);

        //     var normalized = NormalizeUriWithMissingSchemeSeparators(trimmedInput);
        //     AddCandidate(candidates, seen, normalized);
        //     AddCandidate(candidates, seen, $"http://{trimmedInput}");
        //     AddCandidate(candidates, seen, $"https://{trimmedInput}");

        //     return candidates;
        // }

        // private static void AddCandidate(ICollection<string> candidates, ISet<string> seen, string candidate)
        // {
        //     if (!string.IsNullOrWhiteSpace(candidate) && seen.Add(candidate))
        //     {
        //         candidates.Add(candidate);
        //     }
        // }

        // private static string NormalizeUriWithMissingSchemeSeparators(string userInput)
        // {
        //     var schemeSeparatorIndex = userInput.IndexOf(':');
        //     if (schemeSeparatorIndex <= 0)
        //     {
        //         return userInput;
        //     }

        //     var scheme = userInput.Substring(0, schemeSeparatorIndex);
        //     if (!Uri.CheckSchemeName(scheme))
        //     {
        //         return userInput;
        //     }

        //     var remainder = userInput.Substring(schemeSeparatorIndex + 1);
        //     if (remainder.StartsWith("//", StringComparison.Ordinal))
        //     {
        //         return userInput;
        //     }

        //     return $"{scheme}://{remainder.TrimStart('/')}";
        // }

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
