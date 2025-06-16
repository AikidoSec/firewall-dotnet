using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core.Helpers
{
    public static class RouteHelper
    {

        private static readonly string[] ExcludedMethods = { "OPTIONS", "HEAD" };
        private static readonly string[] IgnoreExtensions = { "properties", "config", "webmanifest" };
        private static readonly string[] AllowedExtensions = { "asp", "aspx", "ashx", "asmx", "axd", "asx" };
        private static readonly string[] IgnoreStrings = { "cgi-bin" };
        private static readonly HashSet<string> WellKnownURIs = new HashSet<string>
        {
            "/.well-known/acme-challenge",
            "/.well-known/amphtml",
            "/.well-known/api-catalog",
            "/.well-known/appspecific",
            "/.well-known/ashrae",
            "/.well-known/assetlinks.json",
            "/.well-known/broadband-labels",
            "/.well-known/brski",
            "/.well-known/caldav",
            "/.well-known/carddav",
            "/.well-known/change-password",
            "/.well-known/cmp",
            "/.well-known/coap",
            "/.well-known/coap-eap",
            "/.well-known/core",
            "/.well-known/csaf",
            "/.well-known/csaf-aggregator",
            "/.well-known/csvm",
            "/.well-known/did.json",
            "/.well-known/did-configuration.json",
            "/.well-known/dnt",
            "/.well-known/dnt-policy.txt",
            "/.well-known/dots",
            "/.well-known/ecips",
            "/.well-known/edhoc",
            "/.well-known/enterprise-network-security",
            "/.well-known/enterprise-transport-security",
            "/.well-known/est",
            "/.well-known/genid",
            "/.well-known/gnap-as-rs",
            "/.well-known/gpc.json",
            "/.well-known/gs1resolver",
            "/.well-known/hoba",
            "/.well-known/host-meta",
            "/.well-known/host-meta.json",
            "/.well-known/hosting-provider",
            "/.well-known/http-opportunistic",
            "/.well-known/idp-proxy",
            "/.well-known/jmap",
            "/.well-known/keybase.txt",
            "/.well-known/knx",
            "/.well-known/looking-glass",
            "/.well-known/masque",
            "/.well-known/matrix",
            "/.well-known/mercure",
            "/.well-known/mta-sts.txt",
            "/.well-known/mud",
            "/.well-known/nfv-oauth-server-configuration",
            "/.well-known/ni",
            "/.well-known/nodeinfo",
            "/.well-known/nostr.json",
            "/.well-known/oauth-authorization-server",
            "/.well-known/oauth-protected-resource",
            "/.well-known/ohttp-gateway",
            "/.well-known/openid-federation",
            "/.well-known/open-resource-discovery",
            "/.well-known/openid-configuration",
            "/.well-known/openorg",
            "/.well-known/oslc",
            "/.well-known/pki-validation",
            "/.well-known/posh",
            "/.well-known/privacy-sandbox-attestations.json",
            "/.well-known/private-token-issuer-directory",
            "/.well-known/probing.txt",
            "/.well-known/pvd",
            "/.well-known/rd",
            "/.well-known/related-website-set.json",
            "/.well-known/reload-config",
            "/.well-known/repute-template",
            "/.well-known/resourcesync",
            "/.well-known/sbom",
            "/.well-known/security.txt",
            "/.well-known/ssf-configuration",
            "/.well-known/sshfp",
            "/.well-known/stun-key",
            "/.well-known/terraform.json",
            "/.well-known/thread",
            "/.well-known/time",
            "/.well-known/timezone",
            "/.well-known/tdmrep.json",
            "/.well-known/tor-relay",
            "/.well-known/tpcd",
            "/.well-known/traffic-advice",
            "/.well-known/trust.txt",
            "/.well-known/uma2-configuration",
            "/.well-known/void",
            "/.well-known/webfinger",
            "/.well-known/webweaver.json",
            "/.well-known/wot",
        };

        /// <summary>
        /// Matches a route pattern against an actual URL path
        /// </summary>
        /// <param name="pattern">The route pattern (e.g. "api/users/{id}")</param>
        /// <param name="path">The actual URL path to match against</param>
        /// <returns>True if the path matches the pattern, false otherwise</returns>
        public static bool MatchRoute(string pattern, string path)
        {
            // Convert strings to spans and trim leading slashes for consistent comparison
            ReadOnlySpan<char> patternSpan = pattern.TrimStart('/').AsSpan();
            ReadOnlySpan<char> pathSpan = path.TrimStart('/').AsSpan();

            // Remove query and fragment parts from the pattern and path
            int patternQueryIndex = patternSpan.IndexOf('?');
            int patternFragmentIndex = patternSpan.IndexOf('#');
            int patternEndIndex = patternSpan.Length;

            if (patternQueryIndex >= 0 && patternFragmentIndex >= 0)
                patternEndIndex = Math.Min(patternQueryIndex, patternFragmentIndex);
            else if (patternQueryIndex >= 0)
                patternEndIndex = patternQueryIndex;
            else if (patternFragmentIndex >= 0)
                patternEndIndex = patternFragmentIndex;

            patternSpan = patternSpan.Slice(0, patternEndIndex);

            int pathQueryIndex = pathSpan.IndexOf('?');
            int pathFragmentIndex = pathSpan.IndexOf('#');
            int pathEndIndex = pathSpan.Length;

            if (pathQueryIndex >= 0 && pathFragmentIndex >= 0)
                pathEndIndex = Math.Min(pathQueryIndex, pathFragmentIndex);
            else if (pathQueryIndex >= 0)
                pathEndIndex = pathQueryIndex;
            else if (pathFragmentIndex >= 0)
                pathEndIndex = pathFragmentIndex;

            pathSpan = pathSpan.Slice(0, pathEndIndex);

            // Continue comparing segments while both pattern and path have content
            while (!patternSpan.IsEmpty && !pathSpan.IsEmpty)
            {
                // Get the next segment from both pattern and path
                // e.g. for "api/users/{id}", first segment is "api"
                var patternSegment = patternSpan.GetNextSegment(out patternSpan);
                var pathSegment = pathSpan.GetNextSegment(out pathSpan);

                // If pattern segment is a route parameter (e.g. {id}),
                // skip comparison since any value is valid
                if (patternSegment.IsRouteParameter())
                    continue;

                // If segments don't match (ignoring case), route doesn't match
                if (!pathSegment.Equals(patternSegment, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // Route only matches if both pattern and path are fully consumed
            return patternSpan.IsEmpty && pathSpan.IsEmpty;
        }

        /// <summary>
        /// Checks if a route segment is a parameter (enclosed in curly braces)
        /// </summary>
        /// <param name="span">The route segment to check</param>
        /// <returns>True if segment is a parameter (e.g. {id}), false otherwise</returns>
        public static bool IsRouteParameter(this ReadOnlySpan<char> span)
            => span.StartsWith("{".AsSpan()) && span.EndsWith("}".AsSpan());


        /// <summary>
        /// Determines if a route should be added based on the context and HTTP status code.
        /// </summary>
        /// <param name="context">The context containing route and method information.</param>
        /// <param name="httpStatusCode">The HTTP status code of the request.</param>
        /// <returns>True if the route should be added, false otherwise.</returns>
        public static bool ShouldAddRoute(Context context, int httpStatusCode)
        {
            // Check for null context
            if (context == null)
            {
                LogHelper.DebugLog(Agent.Logger, "Context is null, skipping route");
                return false;
            }

            // Check if the status code is valid
            bool validStatusCode = httpStatusCode >= 200 && httpStatusCode <= 399;
            if (!validStatusCode)
            {
                LogHelper.DebugLog(Agent.Logger, $"Invalid status code: {httpStatusCode}, skipping route");
                return false;
            }

            // Check if the method is excluded
            if (context.Method == null || ExcludedMethods.Contains(context.Method))
            {
                LogHelper.DebugLog(Agent.Logger, $"Method is null or excluded: {context.Method}, skipping route");
                return false;
            }

            // Check for null or empty route
            if (string.IsNullOrEmpty(context.Route))
            {
                LogHelper.DebugLog(Agent.Logger, "Route is null or empty, skipping route");
                return false;
            }

            // Split the route into segments
            var segments = context.Route.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            // Do not discover routes with dot files like `/path/to/.file` or `/.directory/file`
            // We want to allow discovery of well-known URIs like `/.well-known/acme-challenge`
            if (!IsWellKnownURI(context.Route) && segments.Any(IsDotFile))
            {
                LogHelper.DebugLog(Agent.Logger, "Route contains dot file, skipping route");
                return false;
            }

            if (segments.Any(ContainsIgnoredString))
            {
                LogHelper.DebugLog(Agent.Logger, "Route contains ignored string, skipping route");
                return false;
            }

            // Ensure all segments have allowed extensions
            return segments.All(IsAllowedExtension);
        }

        /// <summary>
        /// Checks if an exact route/URL match exists for the given context in the list of endpoints,
        /// following a specific priority order for method and route/URL matching.
        /// Priority:
        /// 1. Exact URL & Exact Method
        /// 2. Exact Route & Exact Method
        /// 3. Exact URL & Wildcard Method
        /// 4. Exact Route & Wildcard Method
        /// </summary>
        /// <param name="context">The context containing request information.</param>
        /// <param name="endpoints">The list of endpoints to match against.</param>
        /// <param name="endpoint">The highest priority matching endpoint, if found.</param>
        /// <returns>True if an exact route/URL match is found according to the priority rules, false otherwise.</returns>
        public static bool HasExactMatch(Context context, IEnumerable<EndpointConfig> endpoints, out EndpointConfig endpoint)
        {
            endpoint = null;
            if (context == null || string.IsNullOrEmpty(context.Method) || endpoints == null)
            {
                return false;
            }

            var specificMethodEndpoints = endpoints.Where(e => e.Method != "*");
            var wildcardMethodEndpoints = endpoints.Where(e => e.Method == "*");

            // 1. Check for Exact URL & Exact Method
            endpoint = specificMethodEndpoints.FirstOrDefault(e =>
                    e.Method.Equals(context.Method, StringComparison.OrdinalIgnoreCase) &&
                    e.Route.Equals(context.Url, StringComparison.OrdinalIgnoreCase));
            if (endpoint != null) return true;

            // 2. Check for Exact Route & Exact Method
            endpoint = specificMethodEndpoints.FirstOrDefault(e =>
                    e.Method.Equals(context.Method, StringComparison.OrdinalIgnoreCase) &&
                    e.Route.Equals(context.Route, StringComparison.OrdinalIgnoreCase));
            if (endpoint != null) return true;

            // 3. Check for Exact URL & Wildcard Method
            endpoint = wildcardMethodEndpoints.FirstOrDefault(e =>
                    e.Route.Equals(context.Url, StringComparison.OrdinalIgnoreCase));
            if (endpoint != null) return true;

            // 4. Check for Exact Route & Wildcard Method
            endpoint = wildcardMethodEndpoints.FirstOrDefault(e =>
                    e.Route.Equals(context.Route, StringComparison.OrdinalIgnoreCase));

            return endpoint != null;
        }

        /// <summary>
        /// Checks if a path is a single segment
        /// </summary>
        /// <param name="path">The path to check</param>
        /// <returns>True if the path is a single segment, false otherwise</returns>
        public static bool PathIsSingleSegment(string path)
        {
            var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            return segments.Length <= 1;
        }

        /// <summary>
        /// Matches a context against a list of endpoints, finding all matching endpoints
        /// </summary>
        /// <param name="context">The context containing request information</param>
        /// <param name="endpoints">The list of endpoints to match against</param>
        /// <returns>A list of matching endpoints, sorted by specificity</returns>
        public static List<EndpointConfig> MatchEndpoints(Context context, IEnumerable<EndpointConfig> endpoints)
        {
            var matches = new List<EndpointConfig>();

            if (string.IsNullOrEmpty(context?.Method))
            {
                return matches;
            }

            // Check for exact match first
            if (HasExactMatch(context, endpoints, out var exactMatch))
            {
                matches.Add(exactMatch);
            }

            // Filter endpoints by method
            var possible = endpoints.Where(endpoint =>
                endpoint.Method == "*" ||
                endpoint.Method.Equals(context.Method, StringComparison.OrdinalIgnoreCase)
            ).ToList();

            // Sort so exact method matches come before wildcard matches
            possible.Sort((a, b) =>
            {
                if (a.Method == b.Method)
                    return 0;
                if (a.Method == "*")
                    return 1;
                if (b.Method == "*")
                    return -1;
                return 0;
            });

            // Handle wildcard routes if URL is available
            if (!string.IsNullOrEmpty(context.Url))
            {
                var path = TryParseUrlPath(context.Url);
                if (!string.IsNullOrEmpty(path))
                {
                    // Get wildcard routes and sort by specificity (more specific first)
                    var wildcards = possible
                        .Where(endpoint => endpoint.Route.Contains("*"))
                        .OrderByDescending(endpoint => endpoint.Route.Split('*').Length)
                        .ToList();

                    foreach (var wildcard in wildcards)
                    {
                        var regex = new Regex(
                            $"^{Regex.Escape(wildcard.Route).Replace("\\*", "(.*)")}/?$",
                            RegexOptions.IgnoreCase
                        );

                        if (regex.IsMatch(path))
                        {
                            matches.Add(wildcard);
                        }
                    }
                }
            }

            return matches;
        }

        private static bool IsAllowedExtension(string segment)
        {
            string extension = Path.GetExtension(segment);
            if (!string.IsNullOrEmpty(extension))
            {
                extension = extension.TrimStart('.');

                if (AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    LogHelper.DebugLog(Agent.Logger, $"Allowed extension: {extension} for route: {segment}, adding route");
                    return true;
                }

                if (IgnoreExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    LogHelper.DebugLog(Agent.Logger, $"Ignoring extension: {extension} for route: {segment}, skipping route");
                    return false;
                }

                if (extension.Length > 1 && extension.Length < 6)
                {
                    LogHelper.DebugLog(Agent.Logger, $"Ignoring extension: {extension} for route: {segment}, skipping route");
                    return false;
                }
            }
            LogHelper.DebugLog(Agent.Logger, $"No extension found for route: {segment}, adding route");
            return true;
        }

        private static bool IsDotFile(string segment)
        {
            return segment.StartsWith(".") && segment.Length > 1;
        }

        private static bool ContainsIgnoredString(string segment)
        {
            return IgnoreStrings.Any(str => segment.Contains(str));
        }

        private static bool IsWellKnownURI(string path)
        {
            return WellKnownURIs.Contains(path);
        }

        private static string TryParseUrlPath(string url)
        {
            try
            {
                var uri = new Uri(url);
                return uri.AbsolutePath;
            }
            catch
            {
                return url;
            }
        }
    }
}
