using Aikido.Zen.Core;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Microsoft.AspNetCore.Http;
using System.Text;
using System.Web; // Import for HTML encoding

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
            Agent.Instance.SetBlockingMiddlewareInstalled(true);
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

            // block the request if the user or Ip is blocked
            if (!EnvironmentHelper.DryMode && Agent.Instance.Context.IsBlocked(user, aikidoContext.RemoteAddress, routeKey, aikidoContext.UserAgent, out string reason))
            {
                Agent.Instance.Context.AddAbortedRequest();
                context.Response.StatusCode = 403;
                await context.Response.WriteAsync($"Your request is blocked: {HttpUtility.HtmlEncode(reason)}");
                return;
            }

            // Is rate limiting enabled for this route?
            if (agentContext.RateLimitedRoutes.TryGetValue(routeKey, out var rateLimitConfig) && rateLimitConfig.Enabled)
            {
                // should we rate limit this request?
                var remoteAddress = HttpUtility.HtmlEncode(aikidoContext.RemoteAddress); // HTML escape the remote address
                var key = $"{routeKey}:user-or-ip:{user?.Id ?? remoteAddress}";
                if (!RateLimitingHelper.IsAllowed(key, rateLimitConfig.WindowSizeInMS, rateLimitConfig.MaxRequests))
                {
                    Agent.Instance.Context.AddAbortedRequest();
                    context.Response.StatusCode = 429;
                    context.Response.Headers.Add("Retry-After", rateLimitConfig.WindowSizeInMS.ToString());
                    await context.Response.WriteAsync($"You are rate limited by Aikido firewall. (Your IP: {remoteAddress})");
                    return;
                }
            }

            await next(context);
        }
    }
}
