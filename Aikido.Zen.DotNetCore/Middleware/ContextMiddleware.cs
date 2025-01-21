using Aikido.Zen.Core;
using Aikido.Zen.Core.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

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
            // Convert headers and query parameters to dictionaries once
            var queryDictionary = httpContext.Request.Query.ToDictionary(q => q.Key, q => q.Value.ToArray());
            var headersDictionary = httpContext.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToArray());

            var context = new Context
            {
                Url = httpContext.Request.Path.ToString(),
                Method = httpContext.Request.Method,
                Query = queryDictionary,
                Headers = headersDictionary,
                RemoteAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty, // no need to use X-FORWARDED-FOR, .NET Core already handles this
                Cookies = httpContext.Request.Cookies.ToDictionary(c => c.Key, c => c.Value),
                UserAgent = headersDictionary.TryGetValue("User-Agent", out var userAgent) ? userAgent.FirstOrDefault() : string.Empty,
                Source = Environment.Version.Major >= 5 ? "DotNetCore" : "DotNetFramework",
                Route = GetRoute(httpContext),
            };

            // Add request information to the agent, which will collect routes, users and stats
            // every x minutes, this information will be sent to the Zen server as a heartbeat event, and the collected info will be cleared
            Agent.Instance.CaptureInboundRequest(context.User, context.Url, context.Method, context.RemoteAddress);

            try
            {
                var request = httpContext.Request;
                request.EnableBuffering();

                var parsedUserInput = await HttpHelper.ReadAndFlattenHttpDataAsync(
                    queryParams: queryDictionary.ToDictionary(h => h.Key, h => string.Join(',', h.Value)),
                    headers: headersDictionary.ToDictionary(h => h.Key, h => string.Join(',', h.Value)),
                    cookies: context.Cookies,
                    body: request.Body,
                    contentType: request.ContentType,
                    contentLength: request.ContentLength ?? 0
                );

                context.ParsedUserInput = parsedUserInput;
                context.Body = request.Body;
            }
            catch (Exception e)
            {
                // Log the exception details if necessary
                throw;
            }

            httpContext.Items["Aikido.Zen.Context"] = context;
            await next(httpContext);
        }

        private string GetRoute(HttpContext context)
        {
            // we use the .NET core route collection to match against the request path,
            // this way, the routes found by Zen match the routes found by the .NET core
            var path = context.Request.Path.Value;
            var endpoint = _endpoints.FirstOrDefault(e => (e as RouteEndpoint) != null && RouteHelper.MatchRoute((e as RouteEndpoint)!.RoutePattern.RawText, path));
            // remove the leading slash from the route pattern, to ensure we don't distinguish for example between api/users and /api/users
            return (endpoint as RouteEndpoint)?.RoutePattern.RawText.TrimStart('/');
        }
    }
}
