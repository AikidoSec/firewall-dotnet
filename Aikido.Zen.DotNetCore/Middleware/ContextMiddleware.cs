using Aikido.Zen.Core;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Helpers.OpenAPI;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

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
            // Convert headers and query parameters to thread-safe dictionaries
            var queryDictionary = new ConcurrentDictionary<string, string[]>(httpContext.Request.Query.ToDictionary(q => q.Key, q => q.Value.ToArray()));
            var headersDictionary = new ConcurrentDictionary<string, string[]>(httpContext.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToArray()));

            var context = new Context
            {
                Url = httpContext.Request.Path.ToString(),
                Method = httpContext.Request.Method,
                Query = queryDictionary,
                Headers = headersDictionary,
                RemoteAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty, // no need to use X-FORWARDED-FOR, .NET Core already handles this
                Cookies = httpContext.Request.Cookies.ToDictionary(c => c.Key, c => c.Value),
                UserAgent = headersDictionary.TryGetValue("User-Agent", out var userAgent) ? userAgent.FirstOrDefault() ?? string.Empty : string.Empty,
                Source = Environment.Version.Major >= 5 ? "DotNetCore" : "DotNetFramework",
                Route = GetParametrizedRoute(httpContext),
            };

            Agent.Instance.SetContextMiddlewareInstalled(true);

            try
            {
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
                    queryParams: queryDictionary.ToDictionary(h => h.Key, h => string.Join(',', h.Value)),
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
                // Add request information to the agent, which will collect routes, users and stats
                // every x minutes, this information will be sent to the Zen server as a heartbeat event, and the collected info will be cleared
                Agent.Instance.CaptureInboundRequest(context);
            }
            catch (Exception e)
            {
                LogHelper.DebugLog(Agent.Logger, $"AIKIDO: Error capturing request: {e.Message}");
                throw;
            }

            httpContext.Items["Aikido.Zen.Context"] = context;
            await next(httpContext);

            // Capture the response status code and check if the route should be added
            int statusCode = httpContext.Response.StatusCode;
            if (RouteHelper.ShouldAddRoute(context, statusCode))
            {
                Agent.Instance.AddRoute(context);
            }
        }

        internal string GetParametrizedRoute(HttpContext context)
        {
            // Use the .NET core route collection to match against the request path,
            // ensuring the routes found by Zen match those found by the .NET core
            var routePattern = context.Request.Path.Value;
            if (context.Request.Path == null)
            {
                return string.Empty;
            }

            // Find the first endpoint that matches the request path
            var endpoint = _endpoints.FirstOrDefault(e =>
            {
                // Check if the endpoint is a RouteEndpoint
                if (e is RouteEndpoint routeEndpoint)
                {
                    // Use the RouteHelper to check if the route pattern matches the request path
                    // e.g. /api/users/{id} will match /api/users/123
                    return RouteHelper.MatchRoute(routeEndpoint.RoutePattern.RawText, context.Request.Path.Value);
                }
                return false;
            });
            routePattern = (endpoint as RouteEndpoint)?.RoutePattern.RawText
                ?? context.Request.Path;

            // Add a leading slash to the route pattern if not present
            return routePattern != null ? "/" + routePattern.TrimStart('/') : string.Empty;
        }
    }
}
