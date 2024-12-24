using Aikido.Zen.Core;
using Aikido.Zen.Core.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.Routing.Template;
using System;

namespace Aikido.Zen.DotNetCore.Middleware
{
    public class ContextMiddleware : IMiddleware
    {
        private readonly IEnumerable<Endpoint> _endpoints;
        public ContextMiddleware(IEnumerable<EndpointDataSource> endpointSources)
        {
            _endpoints = endpointSources.SelectMany(s => s.Endpoints).ToList();
        }

        public async Task InvokeAsync(HttpContext httpContext, RequestDelegate next)
        {
            // this will be used to check for attacks
            var context = new Context
            {
                Url = httpContext.Request.Path.ToString(),
                Method = httpContext.Request.Method,
                Query = httpContext.Request.Query.ToDictionary(q => q.Key, q => q.Value.ToArray()),
                Headers = httpContext.Request.Headers
                    .ToDictionary(h => h.Key, h => h.Value.ToArray()),
                RemoteAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                Cookies = httpContext.Request.Cookies.ToDictionary(c => c.Key, c => c.Value),
                UserAgent = httpContext.Request.Headers["User-Agent"].ToString(),
                Source = Environment.Version.Major >= 5 ? "DotNetCore" : "DotNetFramework",
                Route = GetRoute(httpContext),
            };

            // no need to use X-FORWARDED-FOR, .NET Core already handles this
            var clientIp = httpContext.Connection.RemoteIpAddress?.ToString();
            // Add request information to the agent, which will collect routes, users and stats
            // every x minutes, this information will be sent to the Zen server as a heartbeat event, and the collected info will be cleared
            Agent.Instance.CaptureInboundRequest(context.User, httpContext.Request.Path, context.Method, clientIp);

            try
            {
                var request = httpContext.Request;
                // allow the body to be read multiple times
                request.EnableBuffering();
                var parsedUserInput = await HttpHelper.ReadAndFlattenHttpDataAsync(
                    queryParams: context.Query.ToDictionary(h => h.Key, h => string.Join(',', h.Value)),
                    headers: context.Headers.ToDictionary(h => h.Key, h => string.Join(',', h.Value)),
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
                var message = e.Message;
                var trace = e.StackTrace;
                throw;
            }

            httpContext.Items["Aikido.Zen.Context"] = context;
            await next(httpContext);
        }

        private string? GetRoute(HttpContext context)
        {
            // we use the .NET core route collection to match against the request path,
            // this way, the routes found by Zen match the routes found by the .NET core
            var path = context.Request.Path.Value;
            var endpoint = _endpoints.FirstOrDefault(e => (e as RouteEndpoint) != null && RouteHelper.MatchRoute((e as RouteEndpoint)!.RoutePattern.RawText, path));
            return (endpoint as RouteEndpoint)?.RoutePattern.RawText;
        }
    }
}
