using Aikido.Zen.Core;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Microsoft.AspNetCore.Http;
using System.Text;

namespace Aikido.Zen.DotNetCore.Middleware
{
    /// <summary>
    /// This middleware is used to block incoming requests based on the firewall's configuration
    /// </summary>
    internal class BlockingMiddleware : IMiddleware
    {
        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var agentContext = Agent.Instance.Context;
            var aikidoContext = context.Items["Aikido.Zen.Context"] as Context;
            if (aikidoContext == null)
            {
                await next(context);
                return;
            }
            var user = context.Items["Aikido.Zen.CurrentUser"] as User;
            var routeKey = $"{aikidoContext.Method}|{aikidoContext.Route}";
            if (user != null)
            {
                Agent.Instance.Context.AddUser(user, ipAddress: context.Connection.RemoteIpAddress?.ToString());
            }

            // if Zen running in dry mode, we do not block any requests
            if (EnvironmentHelper.DryMode)
            {
                await next(context);
                return;
            }

            // block the request if the user is blocked
            if (Agent.Instance.Context.IsBlocked(user, context.Connection.RemoteIpAddress?.ToString(), routeKey))
            {
                Agent.Instance.Context.AddAbortedRequest();
                context.Response.StatusCode = 403;
                await context.Response.WriteAsync("Request blocked");
                return;
            }

            // Is rate limiting enabled for this route?
            if (agentContext.RateLimitedRoutes.TryGetValue(routeKey, out var rateLimitConfig) && rateLimitConfig.Enabled)
            {
                // should we rate limit this request?
                if (!RateLimitingHelper.IsAllowed(user?.Id ?? aikidoContext.RemoteAddress, rateLimitConfig.WindowSizeInMS, rateLimitConfig.MaxRequests))
                {
                    Agent.Instance.Context.AddAbortedRequest();
                    context.Response.StatusCode = 429;
                    context.Response.Headers.Add("Retry-After", rateLimitConfig.WindowSizeInMS.ToString());
                    await context.Response.WriteAsync("Too many requests");
                    return;
                }
            }

            await next(context);
        }
    }
}
