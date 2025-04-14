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
            // if the ip is bypassed, skip the handling of the request
            if (Agent.Instance.Context.BlockList.IsIPBypassed(httpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty) || EnvironmentHelper.IsDisabled)
            {
                // call the next middleware
                await next(httpContext);
                return;
            }
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
                User = httpContext.Items["Aikido.Zen.CurrentUser"] as User
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
                // Add user information to the agent
                // every x minutes, this information will be sent to the Zen server as a heartbeat event, and the collected info will be cleared
                Agent.Instance.CaptureRequestUser(context);
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
                Agent.Instance.IncrementTotalRequestCount();
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

            // Find the most exact endpoint that matches the request path
            var endpoint = _endpoints
                .OfType<RouteEndpoint>()
                .FirstOrDefault(e => e.RoutePattern.RawText == context.Request.Path.Value); // check for exact match first

            if (endpoint == null)
            {
                endpoint = _endpoints
                    .OfType<RouteEndpoint>()
                    .Where(e => RouteHelper.MatchRoute(e.RoutePattern.RawText, context.Request.Path.Value))
                    .OrderByDescending(e => e.RoutePattern.RawText.Count(c => c == '/')) // prioritize more specific routes
                    .ThenBy(e => e.RoutePattern.RawText.Count(c => c == '{')) // prioritize routes with fewer parameters
                    .FirstOrDefault();
            }

            // Get route pattern from endpoint or fallback to other methods
            routePattern = (endpoint as RouteEndpoint)?.RoutePattern.RawText;

            // If no route was found, use RouteParameterHelper as fallback
            if (string.IsNullOrEmpty(routePattern))
            {
                var fullUrl = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}{context.Request.QueryString}";
                routePattern = RouteParameterHelper.BuildRouteFromUrl(fullUrl);

                // If parameterization failed, use the raw path
                if (string.IsNullOrEmpty(routePattern))
                {
                    routePattern = context.Request.Path.Value;
                }
            }

            // Override with raw path if it's a single route parameter
            if (RouteParameterHelper.PathIsSingleRouteParameter(routePattern))
            {
                routePattern = context.Request.Path.Value;
            }

            // Add a leading slash to the route pattern if not present
            return routePattern != null ? "/" + routePattern.TrimStart('/') : string.Empty;
        }
    }
}
