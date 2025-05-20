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

            // Check query parameters
            if (context.Query != null)
            {
                foreach (var param in context.Query)
                {
                    foreach (var value in param.Value)
                    {
                        if (ContainsHostname(value, hostname, port))
                        {
                            return new HostnameLocation
                            {
                                Source = "query",
                                PathToPayload = $"query.{param.Key}",
                                Payload = value,
                                Hostname = hostname,
                                Port = port
                            };
                        }
                    }
                }
            }

            // Check headers
            if (context.Headers != null)
            {
                foreach (var header in context.Headers)
                {
                    foreach (var value in header.Value)
                    {
                        if (ContainsHostname(value, hostname, port))
                        {
                            return new HostnameLocation
                            {
                                Source = "headers",
                                PathToPayload = $"headers.{header.Key}",
                                Payload = value,
                                Hostname = hostname,
                                Port = port
                            };
                        }
                    }
                }
            }

            // Check route parameters
            if (context.RouteParams != null)
            {
                foreach (var param in context.RouteParams)
                {
                    if (ContainsHostname(param.Value, hostname, port))
                    {
                        return new HostnameLocation
                        {
                            Source = "routeParams",
                            PathToPayload = $"routeParams.{param.Key}",
                            Payload = param.Value,
                            Hostname = hostname,
                            Port = port
                        };
                    }
                }
            }

            // Check cookies
            if (context.Cookies != null)
            {
                foreach (var cookie in context.Cookies)
                {
                    if (ContainsHostname(cookie.Value, hostname, port))
                    {
                        return new HostnameLocation
                        {
                            Source = "cookies",
                            PathToPayload = $"cookies.{cookie.Key}",
                            Payload = cookie.Value,
                            Hostname = hostname,
                            Port = port
                        };
                    }
                }
            }

            // Check body if it's a string
            if (context.ParsedBody is string bodyStr)
            {
                if (ContainsHostname(bodyStr, hostname, port))
                {
                    return new HostnameLocation
                    {
                        Source = "body",
                        PathToPayload = "body",
                        Payload = bodyStr,
                        Hostname = hostname,
                        Port = port
                    };
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
    }
}
