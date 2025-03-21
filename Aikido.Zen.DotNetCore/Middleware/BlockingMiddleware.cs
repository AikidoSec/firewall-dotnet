using Aikido.Zen.Core;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Microsoft.AspNetCore.Http;
using System.Text;
using System.Web; // Import for HTML encoding
using System.Linq;
using System.Collections.Generic;

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

            // block the request if the user is blocked
            if (!EnvironmentHelper.DryMode && Agent.Instance.Context.IsBlocked(user, context.Connection?.RemoteIpAddress?.ToString(), routeKey, aikidoContext.UserAgent))
            {
                Agent.Instance.Context.AddAbortedRequest();
                context.Response.StatusCode = 403;
                await context.Response.WriteAsync("Request blocked");
                return;
            }

            // Check if rate limiting should be applied
            var remoteAddress = HttpUtility.HtmlEncode(aikidoContext.RemoteAddress); // HTML escape the remote address
            var userOrIp = user?.Id ?? remoteAddress;

            // Use the helper to check all rate limiting rules
            var (isAllowed, effectiveConfig) = RateLimitingHelper.IsRequestAllowed(routeKey, userOrIp, agentContext.RateLimitedRoutes);

            if (!isAllowed)
            {
                Agent.Instance.Context.AddAbortedRequest();
                context.Response.StatusCode = 429;

                if (effectiveConfig != null)
                {
                    context.Response.Headers.Add("Retry-After", effectiveConfig.WindowSizeInMS.ToString());
                }

                await context.Response.WriteAsync($"You are rate limited by Aikido firewall. (Your IP: {remoteAddress})");
                return;
            }

            await next(context);
        }
    }
}
