using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Microsoft.AspNetCore.Http;
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
            try
            {
                // if the ip is bypassed, skip the handling of the request
                if (Agent.Instance.Context.BlockList.IsIPBypassed(GetClientIp(httpContext)) || EnvironmentHelper.IsDisabled)
                {
                    // call the next middleware
                    await next(httpContext);
                    return;
                }
            }
            catch (Exception ex)
            {
                LogHelper.ErrorLog(Agent.Logger, $"Error while checking if the ip is bypassed: {ex.Message}");
            }
            if (!TryPrepareContext(httpContext, out var queryDictionary, out var headersDictionary, out var context))
            {
                // if preparing the context failed, we can't capture the request, so we just call the next middleware
                await next(httpContext);
            }
            try
            {
                LogHelper.DebugLog(Agent.Logger, "Capturing request context");

                Agent.Instance.SetContextMiddlewareInstalled(true);


                var request = httpContext.Request;
                request.EnableBuffering();

                // we need to allow SynchronousIO for xml parsing.
                var syncIOFeature = httpContext.Features.Get<IHttpBodyControlFeature>();
                var initialAllowSynchronousIOValue = syncIOFeature?.AllowSynchronousIO;
                request.ContentType ??= string.Empty;
                // since the feature could be missing, we need to check if it's null before accessing it
                if (syncIOFeature?.AllowSynchronousIO != null)
                {
                    if (request.ContentType.Contains("xml") || request.ContentType.Contains("multipart"))
                    {
                        syncIOFeature.AllowSynchronousIO = true;
                    }
                }

                var httpData = await HttpHelper.ReadAndFlattenHttpDataAsync(
                    queryParams: context.Query,
                    headers: headersDictionary.ToDictionary(h => h.Key, h => string.Join(',', h.Value)),
                    cookies: context.Cookies,
                    body: request.Body,
                    contentType: request.ContentType,
                    contentLength: request.ContentLength ?? 0
                );

                // restore the original value of initialAllowSynchronousIOValue
                if (syncIOFeature != null && initialAllowSynchronousIOValue != null)
                {
                    syncIOFeature.AllowSynchronousIO = initialAllowSynchronousIOValue.Value;
                }

                context.ParsedUserInput = httpData.FlattenedData;
                context.Body = request.Body;
                context.ParsedBody = httpData.ParsedBody;
                // Add user information to the agent
                // every x minutes, this information will be sent to the Zen server as a heartbeat event, and the collected info will be cleared
                Agent.Instance.CaptureRequestUser(context);
                httpContext.Items["Aikido.Zen.Context"] = context;
            }
            catch (Exception e)
            {
                LogHelper.ErrorLog(Agent.Logger, $"Error capturing request: {e.Message}");
            }

            await next(httpContext);

            // Capture the response status code and check if the route should be added
            int statusCode = httpContext.Response.StatusCode;
            try
            {
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

        private bool TryPrepareContext(HttpContext httpContext, out ConcurrentDictionary<string, string[]> queryDictionary, out ConcurrentDictionary<string, string[]> headersDictionary, out Context context)
        {
            try
            {
                // Convert headers and query parameters to thread-safe dictionaries
                queryDictionary = new ConcurrentDictionary<string, string[]>(httpContext.Request.Query.ToDictionary(q => q.Key, q => q.Value.ToArray()));
                headersDictionary = new ConcurrentDictionary<string, string[]>(httpContext.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToArray()));
                context = new Context
                {
                    Url = httpContext.Request.Path.ToString(),
                    Method = httpContext.Request.Method,
                    Query = FlattenQueryParameters(httpContext.Request.Query),
                    Headers = FlattenHeaders(httpContext.Request.Headers),
                    RemoteAddress = GetClientIp(httpContext),
                    Cookies = httpContext.Request.Cookies.ToDictionary(c => c.Key, c => c.Value),
                    UserAgent = httpContext.Request.Headers.TryGetValue("User-Agent", out var userAgent) ? userAgent.FirstOrDefault() ?? string.Empty : string.Empty,
                    Source = Environment.Version.Major >= 5 ? "DotNetCore" : "DotNetFramework",
                    Route = GetParametrizedRoute(httpContext),
                    User = httpContext.Items["Aikido.Zen.CurrentUser"] as User
                };
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.ErrorLog(Agent.Logger, $"Error preparing context: {ex.Message}");
                queryDictionary = null;
                headersDictionary = null;
                context = null;
                return false;
            }

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
                if (values.Count == 1)
                {
                    result[kvp.Key] = values[0];
                }
                else
                {
                    // Example: for ?foo=a&foo=b, the dictionary will be:
                    // { "foo": "a", "foo[1]": "b" }
                    // The first value ("a") is used as the default ("foo"), matching ASP.NET Core's default behavior for query and header collections.
                    for (int i = 0; i < values.Count; i++)
                    {
                        string dictKey = i == 0 ? kvp.Key : $"{kvp.Key}[{i}]";
                        result[dictKey] = values[i];
                    }
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
                if (values.Count == 1)
                {
                    result[kvp.Key] = values[0];
                }
                else
                {
                    for (int i = 0; i < values.Count; i++)
                    {
                        string dictKey = i == 0 ? kvp.Key : $"{kvp.Key}[{i}]";
                        result[dictKey] = values[i];
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the client IP address, considering X-Forwarded-For headers for proxied requests.
        /// </summary>
        /// <param name="httpContext">The HTTP context containing the request information</param>
        /// <returns>The client IP address as a string</returns>
        private static string GetClientIp(HttpContext httpContext)
        {
            // Check for X-Forwarded-For header first (for proxied requests)
            if (httpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
            {
                var firstIp = forwardedFor.FirstOrDefault();
                if (!string.IsNullOrEmpty(firstIp))
                {
                    // X-Forwarded-For can contain multiple IPs, take the first one
                    return firstIp.Split(',')[0].Trim();
                }
            }

            // Fall back to the connection's remote IP address
            return httpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
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
            var routePattern = context.Request.Path.Value;

            if (string.IsNullOrEmpty(routePattern))
            {
                return "/";
            }

            // Ensure the request path starts with a slash for consistency
            routePattern = "/" + routePattern.TrimStart('/');

            // Check for an exact match endpoint
            var frameworkRoutes = _endpoints
                .OfType<RouteEndpoint>()
                .Select(e => GetRoutePattern(e))
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

        /// <summary>
        /// Normalizes an endpoint route pattern string by ensuring it starts with a leading slash.
        /// Returns "/" if the endpoint or its pattern is null/empty.
        /// </summary>
        /// <param name="endpoint">The RouteEndpoint to normalize.</param>
        /// <returns>A normalized route pattern string starting with "/".</returns>
        private static string GetRoutePattern(RouteEndpoint endpoint)
        {
            var pattern = endpoint?.RoutePattern.RawText;
            if (string.IsNullOrEmpty(pattern))
            {
                return "/";
            }
            return "/" + pattern.TrimStart('/');
        }
    }
}
