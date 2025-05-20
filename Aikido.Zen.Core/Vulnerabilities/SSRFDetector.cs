using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core.Vulnerabilities
{
    /// <summary>
    /// Detects Server-Side Request Forgery (SSRF) attacks by analyzing outbound HTTP requests.
    /// </summary>
    public static class SSRFDetector
    {
        /// <summary>
        /// Maximum number of redirects to follow before considering it a potential attack.
        /// </summary>
        private const int MaxRedirects = 10;

        /// <summary>
        /// Checks if a URI is potentially vulnerable to SSRF attacks.
        /// </summary>
        /// <param name="uri">The URI to check.</param>
        /// <param name="context">The agent context containing request information.</param>
        /// <param name="operation">The operation being performed.</param>
        /// <returns>An InterceptorResult if an SSRF attack is detected, null otherwise.</returns>
        public static InterceptorResult CheckContextForSSRF(Uri uri, AgentContext context, string operation)
        {
            // Check the initial URI
            var result = CheckUriForSSRF(uri, operation);
            if (result != null)
            {
                return result;
            }

            // Check each URL in the redirect chain
            if (context.OutgoingRequestRedirects != null && context.OutgoingRequestRedirects.Count > 0)
            {
                // Limit the number of redirects to prevent redirect chain attacks
                if (context.OutgoingRequestRedirects.Count > MaxRedirects)
                {
                    return new InterceptorResult
                    {
                        Operation = operation,
                        Kind = "ssrf",
                        Source = "redirect_chain",
                        PathsToPayload = new List<string> { "redirect_chain" },
                        Metadata = new Dictionary<string, string>
                        {
                            { "redirect_count", context.OutgoingRequestRedirects.Count.ToString() },
                            { "max_redirects", MaxRedirects.ToString() }
                        },
                        Payload = "Excessive redirects detected"
                    };
                }

                // Check each URL in the redirect chain
                foreach (var redirect in context.OutgoingRequestRedirects)
                {
                    result = CheckUriForSSRF(redirect.Destination, operation);
                    if (result != null)
                    {
                        // Add redirect chain information to the result
                        result.Metadata["redirect_source"] = redirect.Source.ToString();
                        result.Metadata["redirect_destination"] = redirect.Destination.ToString();
                        return result;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if a URI is potentially vulnerable to SSRF attacks.
        /// </summary>
        /// <param name="uri">The URI to check.</param>
        /// <param name="operation">The operation being performed.</param>
        /// <returns>An InterceptorResult if an SSRF attack is detected, null otherwise.</returns>
        private static InterceptorResult CheckUriForSSRF(Uri uri, string operation)
        {
            var hostname = uri.Host;
            var port = uri.Port;

            // Check if the hostname is a private IP address
            if (IPHelper.IsPrivateOrLocalIp(hostname))
            {
                return new InterceptorResult
                {
                    Operation = operation,
                    Kind = "ssrf",
                    Source = "private_ip",
                    PathsToPayload = new List<string> { "hostname" },
                    Metadata = new Dictionary<string, string>
                    {
                        { "hostname", hostname },
                        { "port", port.ToString() }
                    },
                    Payload = hostname
                };
            }

            // Check if the hostname is a localhost address
            if (hostname.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                hostname.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                hostname.Equals("::1", StringComparison.OrdinalIgnoreCase))
            {
                return new InterceptorResult
                {
                    Operation = operation,
                    Kind = "ssrf",
                    Source = "localhost",
                    PathsToPayload = new List<string> { "hostname" },
                    Metadata = new Dictionary<string, string>
                    {
                        { "hostname", hostname },
                        { "port", port.ToString() }
                    },
                    Payload = hostname
                };
            }

            return null;
        }
    }
}
