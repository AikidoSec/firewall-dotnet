using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;

[assembly: InternalsVisibleTo("Aikido.Zen.Tests.DotNetCore")]
namespace Aikido.Zen.DotNetCore.Middleware
{
    /// <summary>
    /// This middleware is used to capture the context of incoming requests.
    /// </summary>
    public class ContextMiddleware : IMiddleware
    {
        private readonly IEnumerable<Endpoint> _endpoints;
        public ContextMiddleware(IEnumerable<EndpointDataSource> endpointSources)
        {
            _endpoints = endpointSources.SelectMany(s => s.Endpoints).ToList();
        }

        public async Task InvokeAsync(HttpContext httpContext, RequestDelegate next)
        {
            Context context = null;

            if (!EnvironmentHelper.IsDisabled)
            {
                try
                {
                    var remoteAddress = GetClientIp(httpContext);
                    if (Agent.Instance.Context.Config.BlockList.IsIPBypassed(remoteAddress))
                    {
                        // Store bypass marker context so patches can still honor bypass.
                        context = new Context { Bypassed = true };
                    }
                    else
                    {
                        context = await CaptureContextAsync(httpContext, remoteAddress);
                    }

                    httpContext.Items["Aikido.Zen.Context"] = context;
                }
                catch (Exception e)
                {
                    LogHelper.ErrorLog(Agent.Logger, $"Error capturing request: {e.Message}");
                }
            }

            try
            {
                await next(httpContext);
            }
            finally
            {
                if (!Context.IsNullOrBypassed(context))
                {
                    // Capture the final user once, including users set after the Zen middleware.
                    Agent.Instance.CaptureUser(context.User, context.RemoteAddress);
                    HandleCompletedRequest(context, httpContext.Response.StatusCode);
                }
            }
        }

        private static void HandleCompletedRequest(Context context, int statusCode)
        {
            try
            {
                var attackWaveDetector = Agent.Instance.AttackWaveDetector;
                if (attackWaveDetector.Check(context, statusCode))
                {
                    var samples = attackWaveDetector.GetSamplesForIp(context.RemoteAddress);
                    Agent.Instance.SendAttackWaveEvent(context, samples);
                }

                if (RouteHelper.ShouldAddRoute(context, statusCode))
                {
                    LogHelper.DebugLog(Agent.Logger, "Adding route");
                    Agent.Instance.AddRoute(context);
                    Agent.Instance.IncrementTotalRequestCount();
                }
            }
            catch (Exception ex)
            {
                LogHelper.ErrorLog(Agent.Logger, $"Error adding route: {ex.Message}");
            }
        }

        private async Task<Context> CaptureContextAsync(HttpContext httpContext, string remoteAddress)
        {
            LogHelper.DebugLog(Agent.Logger, "Capturing request context");
            Agent.Instance.SetContextMiddlewareInstalled(true);

            var request = httpContext.Request;
            var query = FlattenQueryParameters(request.Query);
            var headers = FlattenHeaders(request.Headers);
            var cookies = request.Cookies.ToDictionary(c => c.Key, c => c.Value);
            var routeParams = FlattenRouteParameters(httpContext.GetRouteData().Values);
            request.EnableBuffering();

            // we need to allow SynchronousIO for xml parsing.
            var syncIOFeature = httpContext.Features.Get<IHttpBodyControlFeature>();
            var initialAllowSynchronousIOValue = syncIOFeature?.AllowSynchronousIO ?? false;
            request.ContentType ??= string.Empty;
            try
            {
                if (syncIOFeature != null &&
                    (request.ContentType.IndexOf("xml", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     request.ContentType.IndexOf("multipart", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    syncIOFeature.AllowSynchronousIO = true;
                }

                var httpData = await HttpHelper.ReadAndFlattenHttpDataAsync(
                    routeParams: routeParams,
                    queryParams: query,
                    headers: request.Headers.ToDictionary(h => h.Key, h => string.Join(',', h.Value.ToArray())),
                    cookies: cookies,
                    body: request.Body,
                    contentType: request.ContentType
                );

                return new Context
                {
                    Url = request.GetDisplayUrl(),
                    Path = request.Path.ToString(),
                    Method = request.Method,
                    Query = query,
                    Headers = headers,
                    RemoteAddress = remoteAddress,
                    Cookies = cookies,
                    UserAgent = request.Headers.TryGetValue("User-Agent", out var userAgent) ? userAgent.FirstOrDefault() ?? string.Empty : string.Empty,
                    Source = Environment.Version.Major >= 5 ? "DotNetCore" : "DotNetFramework",
                    Route = GetParametrizedRoute(httpContext),
                    RouteParams = routeParams,
                    User = httpContext.Items["Aikido.Zen.CurrentUser"] as User,
                    RateLimitGroup = httpContext.Items["Aikido.Zen.RateLimitGroup"] as string ?? string.Empty,
                    Body = request.Body,
                    ParsedBody = httpData.ParsedBody,
                    ParsedUserInput = httpData.FlattenedData
                };
            }
            finally
            {
                if (syncIOFeature != null)
                {
                    syncIOFeature.AllowSynchronousIO = initialAllowSynchronousIOValue;
                }
            }
        }

        private static IDictionary<string, string> FlattenRouteParameters(RouteValueDictionary routeValues)
        {
            var result = new Dictionary<string, string>();

            foreach (var kvp in routeValues)
            {
                if (kvp.Value == null)
                {
                    continue;
                }

                var value = Convert.ToString(kvp.Value, CultureInfo.InvariantCulture);
                if (value == null)
                {
                    continue;
                }

                result[kvp.Key] = value;
            }

            return result;
        }

        /// <summary>
        /// Flattens query parameters into individual dictionary entries with indexing for multiple values.
        /// </summary>
        /// <param name="query">The query collection</param>
        /// <returns>A dictionary with flattened query parameters</returns>
        private static IDictionary<string, string> FlattenQueryParameters(IQueryCollection query)
        {
            var result = new Dictionary<string, string>();

            foreach (var kvp in query)
            {
                var values = kvp.Value;

                // Example: for ?foo=a&foo=b, the dictionary will be:
                // { "foo": "a", "foo[1]": "b" }
                // The first value ("a") is used as the default ("foo"), matching ASP.NET Core's default behavior for query and header collections.
                for (int i = 0; i < values.Count; i++)
                {
                    string dictKey = i == 0 ? kvp.Key : $"{kvp.Key}[{i}]";
                    result[dictKey] = values[i];
                }
            }

            return result;
        }

        /// <summary>
        /// Flattens headers into individual dictionary entries with indexing for multiple values.
        /// </summary>
        /// <param name="headers">The headers collection</param>
        /// <returns>A dictionary with flattened headers</returns>
        private static IDictionary<string, string> FlattenHeaders(IHeaderDictionary headers)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in headers)
            {
                var values = kvp.Value;
                // Example: for X-Forwarded-For: 1.2.3.4, 5.6.7.8, the dictionary will be:
                // { "X-Forwarded-For": "1.2.3.4", "X-Forwarded-For[1]": "5.6.7.8" }
                for (int i = 0; i < values.Count; i++)
                {
                    string dictKey = i == 0 ? kvp.Key : $"{kvp.Key}[{i}]";
                    result[dictKey] = values[i];
                }

            }

            return result;
        }

        /// <summary>
        /// Gets the client IP address, considering X-Forwarded-For headers for proxied requests.
        /// See https://help.aikido.dev/zen-firewall/zen-installation-instructions/proxy-load-balancer-settings#additional-configuration-for-aspnet-core
        /// </summary>
        /// <param name="httpContext">The HTTP context containing the request information</param>
        /// <returns>The client IP address as a string</returns>
        private static string GetClientIp(HttpContext httpContext)
        {
            // RemoteIpAddress was almost good enough to return directly, as it already contains either:
            //   - the regular connection remote ip, or
            //   - the last ip entry in the X-Forwarded-For header (which also gets removed from the X-Forwarded-For list)
            // However, the standard specifies the client's ip to be the first entry in the X-Forwarded-For list.

            if (EnvironmentHelper.TrustProxy)
            {
                // Usually X-Forwarded-For, but can be set to something else via AIKIDO_CLIENT_IP_HEADER
                var headerVarName = EnvironmentHelper.ClientIpHeader;
                if (httpContext.Request?.Headers?.TryGetValue(headerVarName, out var ipStringValues) == true)
                {
                    // X-Forwarded-For can contain multiple IPs
                    var ipHeader = ipStringValues.ToString();
                    var ipList = IPHeaderHelper.ParseIpHeader(ipHeader);

                    foreach (var ip in ipList)
                    {
                        // Return the first valid non-private IP address
                        if (IPHelper.IsValidIp(ip) && !IPHelper.IsPrivateOrLocalIp(ip))
                        {
                            return ip;
                        }
                    }
                }
            }

            // No X-Forwarded-For header, or X-Forwarded-For is present and ASP NET already popped the last list entry in RemoteIpAddress
            return httpContext.Connection?.RemoteIpAddress?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// Gets a parameterized route from the HTTP context, matching against configured endpoints
        /// and applying route parameter detection when needed, while prioritizing specific paths
        /// over overly generic route patterns (e.g., prefers `/users/123` over `/{slug}`).
        /// </summary>
        /// <param name="context">The HTTP context containing the request information</param>
        /// <returns>A parameterized route string, always starting with a leading slash</returns>
        internal string GetParametrizedRoute(HttpContext context)
        {
            var routePattern = RouteHelper.NormalizeRoutePattern(context.Request.Path.Value);

            // Check for an exact match endpoint
            var frameworkRoutes = _endpoints
                .OfType<RouteEndpoint>()
                .Select(e => RouteHelper.NormalizeRoutePattern(e?.RoutePattern.RawText))
                .ToList();

            var exactEndpoint = frameworkRoutes.FirstOrDefault(rp => rp == routePattern);

            if (exactEndpoint != null)
            {
                // for too generic routes, we prefer to use the exact match route
                // since some applications create a catch-all route for all requests e.g. /{slug}
                if (RouteHelper.PathIsSingleSegment(exactEndpoint))
                {
                    return routePattern;
                }
                return exactEndpoint;
            }
            else
            {
                // 2. No exact match, find the best matching endpoint based on specificity and parameter count
                var bestMatchedRoute = frameworkRoutes
                    .Where(e => RouteHelper.MatchRoute(e, routePattern))
                    .OrderByDescending(e => e.Count(c => c == '/')) // prioritize more specific routes (more '/')
                    .ThenBy(e => e.Count(c => c == '{')) // prioritize routes with fewer parameters
                    .FirstOrDefault();

                if (bestMatchedRoute != null)
                {
                    // for too generic routes, we prefer to use the exact match route
                    // since some applications create a catch-all route for all requests e.g. /{slug}
                    if (RouteHelper.PathIsSingleSegment(bestMatchedRoute))
                    {
                        return routePattern;
                    }
                    return bestMatchedRoute;
                }

                var parameterizedRoute = RouteParameterHelper.BuildRouteFromUrl(routePattern);
                if (!string.IsNullOrEmpty(parameterizedRoute))
                {
                    routePattern = parameterizedRoute;
                }

                return routePattern;
            }
        }
    }
}
