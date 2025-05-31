using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// Provides operations related to context and input validation.
    /// </summary>
    public static class ContextHelper
    {
        /// <summary>
        /// Checks if a request is being made to the same server.
        /// </summary>
        /// <param name="context">The current request context.</param>
        /// <param name="outboundHostname">The hostname of the outbound request.</param>
        /// <param name="outboundPort">The port of the outbound request.</param>
        /// <returns>True if the request is to the same server, false otherwise.</returns>
        public static bool IsRequestToItself(Context context, string outboundHostname, int? outboundPort)
        {
            if (context == null || string.IsNullOrEmpty(outboundHostname))
                return false;

            var serverUrl = context.Url;
            if (string.IsNullOrEmpty(serverUrl))
                return false;

            var uri = new Uri(serverUrl);
            var serverHostname = uri.Host;
            var serverPort = uri.Port;

            // Check if hostnames match
            if (!string.Equals(serverHostname, outboundHostname, StringComparison.OrdinalIgnoreCase))
                return false;

            // If outbound port is specified, check if it matches server port
            if (outboundPort.HasValue)
                return outboundPort.Value == serverPort;

            // If outbound port is not specified, assume default port (80 for HTTP, 443 for HTTPS)
            var defaultPort = uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? 443 : 80;
            return serverPort == defaultPort;
        }

        /// <summary>
        /// Inspects the context for occurrences of a specified hostname.
        /// </summary>
        /// <param name="context">The current request context.</param>
        /// <param name="hostname">The hostname to look for.</param>
        /// <param name="port">The port to look for.</param>
        /// <returns>A HostnameLocation object if found, null otherwise.</returns>
        public static HostnameLocation FindHostnameInContext(Context context, string hostname, int? port = null)
        {
            if (context == null || string.IsNullOrEmpty(hostname))
                return null;

            // Check parsed user input
            if (context.ParsedUserInput != null)
            {
                foreach (var input in context.ParsedUserInput)
                {
                    if (ContainsHostname(input.Value, hostname, port))
                    {
                        return new HostnameLocation
                        {
                            Source = input.Key.Split('.')[0],
                            PathToPayload = input.Key,
                            Payload = input.Value,
                            Hostname = hostname,
                            Port = port
                        };
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if a value contains the specified hostname and port.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <param name="hostname">The hostname to look for.</param>
        /// <param name="port">The port to look for.</param>
        /// <returns>True if the value contains the hostname and port, false otherwise.</returns>
        private static bool ContainsHostname(string value, string hostname, int? port)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(hostname))
                return false;

            // If port is specified, look for hostname:port
            if (port.HasValue)
            {
                var hostnameWithPort = $"{hostname}:{port.Value}";
                return value.IndexOf(hostnameWithPort, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            // Otherwise just look for the hostname
            return value.IndexOf(hostname, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Checks if a hostname exists in user input.
        /// </summary>
        /// <param name="userInput">The user input to check.</param>
        /// <param name="hostname">The hostname to look for.</param>
        /// <param name="port">The port to look for.</param>
        /// <returns>True if the hostname is found in the user input, false otherwise.</returns>
        public static bool FindHostnameInUserInput(string userInput, string hostname, int? port)
        {
            // early return if our userInput clearly is not a URL
            if (string.IsNullOrEmpty(userInput) || string.IsNullOrEmpty(hostname) || userInput.Length <= 1)
                return false;

            // Try parsing with http:// prefix
            if (!Uri.TryCreate($"http://{hostname}" + (port.HasValue ? $":{port}" : ""), UriKind.Absolute, out _))
                return false;

            // Create variants to check
            var variants = new[]
            {
                userInput,
                $"http://{userInput}",
                $"https://{userInput}"
            };

            foreach (var variant in variants)
            {
                if (!Uri.TryCreate(variant, UriKind.Absolute, out var variantUri))
                    continue;

                if (!port.HasValue)
                    return true;

                if (variantUri.Port == port.Value)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the origin URL of a redirect chain.
        /// </summary>
        /// <param name="context">The current request context.</param>
        /// <param name="url">The URL to find the origin for.</param>
        /// <returns>The origin URL of the redirect chain, or null if not found.</returns>
        public static Uri GetRedirectOrigin(Context context, Uri url)
        {
            if (context?.OutgoingRequestRedirects == null || url == null)
                return null;

            var currentUrl = url;
            var visited = new HashSet<string>();

            while (true)
            {
                var currentUrlStr = currentUrl.ToString();
                if (visited.Contains(currentUrlStr))
                    break;

                visited.Add(currentUrlStr);

                var matchingRedirect = context.OutgoingRequestRedirects
                    .FirstOrDefault(r => r.Destination.ToString().Equals(currentUrlStr, StringComparison.OrdinalIgnoreCase));

                if (matchingRedirect.Source == null)
                    break;

                currentUrl = matchingRedirect.Source;
            }

            return currentUrl;
        }
    }
}
