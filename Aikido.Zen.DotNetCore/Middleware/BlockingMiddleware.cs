using System.Collections.Generic;
using System.Linq;
using System.Web; // Import for HTML encoding
using Aikido.Zen.Core;
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
        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            LogHelper.DebugLog(Agent.Logger, "Checking if request should be blocked");
            var agentContext = Agent.Instance.Context;
            var aikidoContext = context.Items["Aikido.Zen.Context"] as Context;
            Agent.Instance.SetBlockingMiddlewareInstalled(true);

            // if the context is not found, skip the blocking checks, this likely means that the request is bypassed
            if (aikidoContext == null)
            {
                // call the next middleware
                await next(context);
                return;
            }
            var user = context.Items["Aikido.Zen.CurrentUser"] as User;
            if (user != null)
            {
                Agent.Instance.Context.AddUser(user, ipAddress: context.Connection.RemoteIpAddress?.ToString());
            }

            // block the request if the user is blocked
            if (Agent.Instance.Context.IsBlocked(aikidoContext, out var reason))
            {
                Agent.Instance.Context.AddAbortedRequest();
                context.Response.StatusCode = 403;
                LogHelper.DebugLog(Agent.Logger, $"Request blocked: {HttpUtility.HtmlEncode(reason)}");
                await context.Response.WriteAsync($"Your request is blocked: {HttpUtility.HtmlEncode(reason)}");
                return;
            }

            // Check if rate limiting should be applied
            var remoteAddress = HttpUtility.HtmlEncode(aikidoContext.RemoteAddress); // HTML escape the remote address

            // Use the helper to check all rate limiting rules
            var (isAllowed, effectiveConfig) = RateLimitingHelper.IsRequestAllowed(aikidoContext, agentContext.Endpoints);

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
