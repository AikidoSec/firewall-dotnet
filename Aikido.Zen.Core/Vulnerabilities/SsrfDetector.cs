using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core.Vulnerabilities
{
    internal interface IDnsResolver
    {
        string[] ResolveHostAddresses(string hostname);
    }

    internal sealed class DefaultDnsResolver : IDnsResolver
    {
        public string[] ResolveHostAddresses(string hostname)
        {
            try
            {
                return Dns.GetHostAddresses(hostname)
                    .Select(address => address.ToString())
                    .ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }
    }

    internal static class ImdsHelper
    {
        private static readonly HashSet<string> TrustedHostnames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "metadata.google.internal",
            "metadata.goog",
        };

        private static readonly HashSet<string> ImdsIPv4 = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "169.254.169.254",
            "100.100.100.200",
        };

        private static readonly HashSet<string> ImdsIPv6 = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "fd00:ec2::254",
        };

        internal static bool IsTrustedHostname(string hostname)
        {
            return !string.IsNullOrWhiteSpace(hostname) && TrustedHostnames.Contains(hostname.Trim());
        }

        internal static bool IsImdsIp(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip) || !IPAddress.TryParse(ip, out var parsedIp))
            {
                return false;
            }

            if (parsedIp.IsIPv4MappedToIPv6)
            {
                parsedIp = parsedIp.MapToIPv4();
            }

            var normalized = parsedIp.ToString();
            return ImdsIPv4.Contains(normalized) || ImdsIPv6.Contains(normalized);
        }
    }

    internal sealed class SsrfDetectionResult
    {
        public AttackKind Kind { get; set; }
        public Source? Source { get; set; }
        public string Payload { get; set; }
        public IDictionary<string, object> Metadata { get; set; }
    }

    internal static class SsrfDetector
    {
        private static readonly Regex ServiceHostnamePattern = new Regex("^[a-z-_]+$", RegexOptions.Compiled);
        private static readonly HashSet<string> NotServiceHostnames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "localhost",
            "localdomain",
            "metadata",
        };
        private static readonly LRUCache<string, string[]> DnsCache = new LRUCache<string, string[]>(1000, 60000);
        private static IDnsResolver _dnsResolver = new DefaultDnsResolver();

        internal static void SetDnsResolver(IDnsResolver dnsResolver)
        {
            _dnsResolver = dnsResolver ?? new DefaultDnsResolver();
            DnsCache.Clear();
        }

        internal static void ResetDnsResolver()
        {
            _dnsResolver = new DefaultDnsResolver();
            DnsCache.Clear();
        }

        internal static SsrfDetectionResult Detect(Uri targetUri, Context context)
        {
            if (targetUri == null)
            {
                return null;
            }

            return DetectDirectSsrf(targetUri, context) ?? DetectDnsSsrf(targetUri, context);
        }

        private static SsrfDetectionResult DetectDirectSsrf(Uri targetUri, Context context)
        {
            var hostname = targetUri.Host;
            if (!ContainsPrivateOrLocalHostname(hostname))
            {
                return null;
            }

            var match = FindHostnameInContext(context, hostname, UriHelper.GetPort(targetUri));
            if (match == null)
            {
                return null;
            }

            return new SsrfDetectionResult
            {
                Kind = AttackKind.Ssrf,
                Source = match.Source,
                Payload = match.Payload,
                Metadata = GetMetadataForSsrfAttack(hostname, UriHelper.GetPort(targetUri), null),
            };
        }

        private static SsrfDetectionResult DetectDnsSsrf(Uri targetUri, Context context)
        {
            var hostname = targetUri.Host;
            var normalizedHostname = NormalizeHostname(hostname);
            var port = UriHelper.GetPort(targetUri);

            if (IPAddress.TryParse(normalizedHostname, out _))
            {
                return null;
            }

            if (IsRequestToServiceHostname(hostname))
            {
                return null;
            }

            var resolvedAddresses = ResolveHostAddresses(normalizedHostname);
            if (resolvedAddresses.Length == 0)
            {
                return null;
            }

            var privateIp = FindPrivateIp(resolvedAddresses);
            if (privateIp == null)
            {
                return null;
            }

            var match = FindHostnameInContext(context, hostname, port);
            if (match != null)
            {
                return new SsrfDetectionResult
                {
                    Kind = AttackKind.Ssrf,
                    Source = match.Source,
                    Payload = match.Payload,
                    Metadata = GetMetadataForSsrfAttack(hostname, port, privateIp),
                };
            }

            if (ImdsHelper.IsTrustedHostname(normalizedHostname))
            {
                return null;
            }

            var imdsIp = FindImdsIp(resolvedAddresses);
            if (imdsIp == null)
            {
                return null;
            }

            return new SsrfDetectionResult
            {
                Kind = AttackKind.StoredSsrf,
                Source = null,
                Payload = null,
                Metadata = GetMetadataForSsrfAttack(hostname, null, imdsIp),
            };
        }

        private static HostnameMatch FindHostnameInContext(Context context, string hostname, int? port)
        {
            if (context == null || context.ParsedUserInput == null)
            {
                return null;
            }

            if (IsRequestToServiceHostname(hostname))
            {
                return null;
            }

            if (IsRequestToItself(context.Url, hostname, port))
            {
                return null;
            }

            foreach (var userInput in context.ParsedUserInput)
            {
                if (string.IsNullOrWhiteSpace(userInput.Value))
                {
                    continue;
                }

                if (FindHostnameInUserInput(userInput.Value, hostname, port))
                {
                    return new HostnameMatch
                    {
                        Payload = userInput.Value,
                        Source = HttpHelper.GetSourceFromUserInputPath(userInput.Key),
                    };
                }
            }

            return null;
        }

        private static bool FindHostnameInUserInput(string userInput, string hostname, int? port)
        {
            if (string.IsNullOrWhiteSpace(userInput) || userInput.Length <= 1)
            {
                return false;
            }

            var normalizedHostname = NormalizeHostname(hostname);
            var variants = new[]
            {
                userInput,
                $"http://{userInput}",
                $"https://{userInput}",
            };

            foreach (var variant in variants)
            {
                if (!Uri.TryCreate(variant, UriKind.Absolute, out var userInputUri))
                {
                    continue;
                }

                if (!string.Equals(NormalizeHostname(userInputUri.Host), normalizedHostname, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!port.HasValue)
                {
                    return true;
                }

                if (UriHelper.GetPort(userInputUri) == port)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsPrivateOrLocalHostname(string hostname)
        {
            var normalizedHostname = NormalizeHostname(hostname);
            return string.Equals(normalizedHostname, "localhost", StringComparison.OrdinalIgnoreCase) ||
                   IPHelper.IsPrivateOrLocalIp(normalizedHostname);
        }

        private static bool IsRequestToItself(string serverUrl, string outboundHostname, int? outboundPort)
        {
            if (!EnvironmentHelper.TrustProxy || string.IsNullOrWhiteSpace(serverUrl) || string.IsNullOrWhiteSpace(outboundHostname))
            {
                return false;
            }

            if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var serverUri))
            {
                return false;
            }

            if (!string.Equals(NormalizeHostname(serverUri.Host), NormalizeHostname(outboundHostname), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var serverPort = UriHelper.GetPort(serverUri);
            if (serverPort == outboundPort)
            {
                return true;
            }

            return (serverPort == 80 && outboundPort == 443) || (serverPort == 443 && outboundPort == 80);
        }

        private static bool IsRequestToServiceHostname(string hostname)
        {
            var lowerHostname = NormalizeHostname(hostname);
            if (NotServiceHostnames.Contains(lowerHostname))
            {
                return false;
            }

            return ServiceHostnamePattern.IsMatch(lowerHostname);
        }

        private static string[] ResolveHostAddresses(string hostname)
        {
            if (DnsCache.TryGetValue(hostname, out var cached))
            {
                return cached ?? Array.Empty<string>();
            }

            var resolved = _dnsResolver.ResolveHostAddresses(hostname) ?? Array.Empty<string>();
            DnsCache.Set(hostname, resolved);
            return resolved;
        }

        private static string FindPrivateIp(IEnumerable<string> addresses)
        {
            foreach (var address in addresses)
            {
                if (IPHelper.IsPrivateOrLocalIp(address))
                {
                    return address;
                }
            }

            return null;
        }

        private static string FindImdsIp(IEnumerable<string> addresses)
        {
            foreach (var address in addresses)
            {
                if (ImdsHelper.IsImdsIp(address))
                {
                    return address;
                }
            }

            return null;
        }

        private static IDictionary<string, object> GetMetadataForSsrfAttack(string hostname, int? port, string privateIp)
        {
            var metadata = new Dictionary<string, object>
            {
                ["hostname"] = hostname,
            };

            if (port.HasValue)
            {
                metadata["port"] = port.Value;
            }

            if (!string.IsNullOrWhiteSpace(privateIp))
            {
                metadata["privateIP"] = privateIp;
            }

            return metadata;
        }

        private static string NormalizeHostname(string hostname)
        {
            return (hostname ?? string.Empty).Trim().Trim('[', ']').ToLowerInvariant();
        }

        private sealed class HostnameMatch
        {
            public Source Source { get; set; }
            public string Payload { get; set; }
        }
    }
}
