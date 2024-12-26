using Aikido.Zen.Core;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Microsoft.AspNetCore.Http;

namespace Aikido.Zen.DotNetCore.Middleware
{
    /// <summary>
    /// This middleware is used to block incoming requests based on the firewall's configuration
    /// </summary>
    internal class BlockingMiddleware : IMiddleware
    {
        public Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var agentContext = Agent.Instance.Context;
            var aikidoContext = context.Items["Aikido.Zen.Context"] as Context;
            if (aikidoContext == null)
            {
                next(context);
            }
            var user = context.Items["Aikido.Zen.CurrentUser"] as User;
            var routeKey = $"{aikidoContext.Method}|{aikidoContext.Route}";
            if (user != null)
            {
                Agent.Instance.Context.AddUser(user, ipAddress: context.Connection.RemoteIpAddress?.ToString());
            }

            // Is rate limiting enabled for this route?
            if (agentContext.RateLimitedRoutes.TryGetValue(routeKey, out var rateLimitConfig) && rateLimitConfig.Enabled)
            {
                // should we rate limit this request?
                if (RateLimitingHelper.IsAllowed(user?.Id ?? aikidoContext.RemoteAddress, rateLimitConfig.WindowSizeInMS, rateLimitConfig.MaxRequests))
                {
                    Agent.Instance.Context.AddAbortedRequest();
                    context.Response.StatusCode = 429;
                    // no need to continue down the pipeline
                    return Task.FromResult(0);
                }
            }
            // block the request if the user is blocked
            if (Agent.Instance.Context.IsBlocked(user, context.Connection.RemoteIpAddress?.ToString(), routeKey))
            {
                Agent.Instance.Context.AddAbortedRequest();
                context.Response.StatusCode = 403;
            }

            return next(context);
        }
    }
}
