using Aikido.Zen.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// Helper class for detecting Server-Side Request Forgery (SSRF) vulnerabilities.
    /// </summary>
    public static class SsrfHelper
    {
        /// <summary>
        /// Detects potential SSRF attempts by checking if user input matches a given hostname and optional port,
        /// especially focusing on requests targeting private or local IP addresses.
        /// </summary>
        /// <param name="hostname">The target hostname of the outgoing request.</param>
        /// <param name="port">The target port of the outgoing request (optional).</param>
        /// <param name="context">The current request context containing user inputs.</param>
        /// <param name="moduleName">The name of the module performing the check.</param>
        /// <param name="operation">The operation being performed (e.g., "HttpRequest").</param>
        /// <returns>True if a potential SSRF attack is detected, false otherwise.</returns>
        public static bool DetectSSRF(string hostname, int? port, Context context, string moduleName, string operation)
        {
            // Optimization: Only check user input if the target hostname is a private/local IP or resolves to one.
            // Direct domain lookups are handled elsewhere. This focuses on direct HTTP requests.
            // Attempt to resolve hostname if it's not already an IP
            IPAddress[] addresses;
            try
            {
                // Handle cases like "localhost"
                if (hostname.Equals("localhost", StringComparison.OrdinalIgnoreCase))
                {
                    addresses = Dns.GetHostAddresses(Dns.GetHostName())
                                    .Concat(new[] { IPAddress.Loopback, IPAddress.IPv6Loopback })
                                    .Distinct()
                                    .ToArray();
                }
                else if (!IPAddress.TryParse(hostname, out _)) // Avoid DNS lookup if already an IP
                {
                    addresses = Dns.GetHostAddresses(hostname);
                }
                else // it's an IP address string
                {
                    addresses = new[] { IPAddress.Parse(hostname) };
                }
            }
            catch (System.Net.Sockets.SocketException)
            {
                // Hostname could not be resolved, unlikely to be a successful SSRF targetting private IPs here
                return false;
            }
            catch (ArgumentException)
            {
                // Invalid hostname format
                return false;
            }


            bool targetsPrivateOrLocal = addresses.Any(addr => IPHelper.IsPrivateOrLocalIp(addr.ToString()));

            if (!targetsPrivateOrLocal)
            {
                return false; // No need to check user input if target isn't private/local
            }

            // Check if the request targets the same host as the incoming request
            // Avoids flagging requests originating from headers like 'Host', 'Origin', etc.
            if (!string.IsNullOrEmpty(context.Url) && Uri.TryCreate(context.Url, UriKind.Absolute, out var incomingUri))
            {
                var incomingHost = incomingUri.Host;
                var incomingPort = incomingUri.Port; // Returns default port if not specified

                if (IsRequestToItself(incomingHost, incomingPort, hostname, addresses, port))
                {
                    return false;
                }
            }


            // Iterate through user-provided inputs
            foreach (var userInputEntry in context.ParsedUserInput)
            {
                string userInputKey = userInputEntry.Key;
                string userInputValue = userInputEntry.Value;

                if (string.IsNullOrWhiteSpace(userInputValue) || userInputValue.Length <= 1)
                {
                    continue;
                }

                if (FindHostnameInUserInput(userInputValue, hostname, addresses, port))
                {
                    var metadata = new Dictionary<string, object>
                    {
                        { "target_hostname", hostname },
                        { "target_port", port?.ToString() ?? "default" },
                        { "resolved_ips", addresses.Select(a => a.ToString()).ToArray() }
                    };

                    // Send an attack event
                    Agent.Instance.SendAttackEvent(
                        kind: AttackKind.Ssrf,
                        source: HttpHelper.GetSourceFromUserInputPath(userInputKey), // Assuming similar helper exists or is needed
                        payload: userInputValue,
                        operation: operation,
                        context: context,
                        module: moduleName,
                        metadata: metadata,
                        blocked: !EnvironmentHelper.DryMode // Assuming similar helper
                    );

                    context.AttackDetected = true;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if an outgoing request targets the same host/IP and port as the server hosting the application.
        /// </summary>
        private static bool IsRequestToItself(string incomingHost, int incomingPort, string outgoingHostOrIp, IPAddress[] outgoingAddresses, int? outgoingPort)
        {
            if (string.IsNullOrEmpty(incomingHost)) return false;

            // Normalize localhost variations for comparison
            bool isIncomingHostLocal = incomingHost.Equals("localhost", StringComparison.OrdinalIgnoreCase) || IPAddress.IsLoopback(IPAddress.Parse(IPHelper.Server)); // Assuming IPHelper.Server gives local IP
            bool isOutgoingHostLocal = outgoingHostOrIp.Equals("localhost", StringComparison.OrdinalIgnoreCase) || outgoingAddresses.Any(IPAddress.IsLoopback);


            // Case 1: Both resolve to loopback/localhost conceptually
            if (isIncomingHostLocal && isOutgoingHostLocal)
            {
                // If ports match (or outgoing port is default and matches implicit incoming port), consider it targeting itself
                return !outgoingPort.HasValue || outgoingPort.Value == incomingPort;
            }

            // Case 2: Compare specific IPs
            IPAddress[] incomingAddresses;
            try
            {
                incomingAddresses = isIncomingHostLocal ?
                    Dns.GetHostAddresses(Dns.GetHostName()).Concat(new[] { IPAddress.Loopback, IPAddress.IPv6Loopback }).Distinct().ToArray() :
                    Dns.GetHostAddresses(incomingHost);
            }
            catch
            {
                incomingAddresses = Array.Empty<IPAddress>(); // Handle resolution failure
            }

            bool ipMatch = incomingAddresses.Intersect(outgoingAddresses).Any();

            if (ipMatch)
            {
                return !outgoingPort.HasValue || outgoingPort.Value == incomingPort;
            }


            return false;
        }


        /// <summary>
        /// Attempts to find if a user-provided string contains a given hostname (or its resolved IPs) and optionally port.
        /// Handles URL parsing variations.
        /// </summary>
        public static bool FindHostnameInUserInput(string userInput, string targetHostname, IPAddress[] targetAddresses, int? targetPort)
        {
            // Normalize target hostname for comparison if it's not an IP
            string normalizedTargetHostname = targetHostname;
            if (!IPAddress.TryParse(targetHostname, out _))
            {
                normalizedTargetHostname = targetHostname.ToLowerInvariant();
            }


            var variants = new List<string> { userInput };
            // Only add scheme if userInput doesn't seem to have one already
            if (!userInput.Contains("://"))
            {
                variants.Add($"http://{userInput}");
                variants.Add($"https://{userInput}");
            }


            foreach (var variant in variants)
            {
                if (TryParseUrl(variant, out Uri parsedUri))
                {
                    string inputHost = parsedUri.Host;
                    int inputPort = parsedUri.Port; // Returns -1 if not specified

                    bool hostnameMatch = false;
                    // 1. Direct hostname match (case-insensitive)
                    if (inputHost.Equals(normalizedTargetHostname, StringComparison.OrdinalIgnoreCase))
                    {
                        hostnameMatch = true;
                    }
                    // 2. Check if inputHost is an IP that matches one of the target resolved IPs
                    else if (IPAddress.TryParse(inputHost, out var inputIpAddress))
                    {
                        if (targetAddresses.Contains(inputIpAddress))
                        {
                            hostnameMatch = true;
                        }
                    }
                    // 3. Check if inputHost resolves to an IP that matches one of the target IPs
                    //    (Avoids excessive DNS lookups unless necessary, already done for target)
                    else
                    {
                        try
                        {
                            var inputResolvedAddresses = Dns.GetHostAddresses(inputHost);
                            if (inputResolvedAddresses.Intersect(targetAddresses).Any())
                            {
                                hostnameMatch = true;
                            }
                        }
                        catch { /* Ignore resolution errors for user input */ }
                    }


                    if (hostnameMatch)
                    {
                        // Host matches, now check port
                        int effectiveInputPort = GetPortFromUriOrDefault(parsedUri); // Handles default ports
                        int effectiveTargetPort = targetPort ?? GetDefaultPort(parsedUri.Scheme); // Use targetPort if specified, else default

                        if (!targetPort.HasValue || effectiveInputPort == effectiveTargetPort)
                        {
                            return true; // Match found
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Safely tries to parse a string into a Uri.
        /// </summary>
        public static bool TryParseUrl(string urlString, out Uri uri)
        {
            // Basic check for plausible URL structure before full parsing attempt
            if (string.IsNullOrWhiteSpace(urlString) || urlString.Length < 3 || urlString.Contains(" "))
            {
                uri = null;
                return false;
            }

            // Uri.TryCreate requires a scheme. If missing, try adding http/https.
            string urlToParse = urlString;
            if (!urlString.Contains("://"))
            {
                // Heuristic: If it contains typical domain chars or IP chars, prepend http.
                // This is imperfect but avoids prepending to relative paths or fragments.
                if (urlString.Contains('.') || urlString.Contains(':') || IPAddress.TryParse(urlString.Split('/')[0], out _)) // crude check for domain/IP like structure
                {
                    urlToParse = "http://" + urlString;
                }
                else
                {
                    // Doesn't look like a host that needs a scheme prepended for parsing host/port
                    uri = null;
                    return false;
                }
            }


            // Use Uri.TryCreate for robustness
            return Uri.TryCreate(urlToParse, UriKind.Absolute, out uri);
        }

        /// <summary>
        /// Gets the port number from a Uri, returning the default port for the scheme if not specified.
        /// </summary>
        public static int GetPortFromUriOrDefault(Uri uri)
        {
            if (uri.Port != -1)
            {
                return uri.Port;
            }
            return GetDefaultPort(uri.Scheme);
        }

        /// <summary>
        /// Gets the default port number for a given URI scheme.
        /// </summary>
        public static int GetDefaultPort(string scheme)
        {
            switch (scheme.ToLowerInvariant())
            {
                case "http":
                    return 80;
                case "https":
                    return 443;
                // Add other schemes if needed (e.g., ftp: 21)
                default:
                    return -1; // Indicate unknown default port
            }
        }
    }
}
