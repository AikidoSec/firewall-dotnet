using System.Web; // Import for HTML encoding

using Microsoft.AspNetCore.Http;

using Aikido.Zen.Core;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.DotNetCore.Middleware;

/// <summary>
/// This middleware is used to block incoming requests based on the firewall's configuration
/// </summary>
internal class BlockingMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            LogHelper.DebugLog(Agent.Logger, "Checking if request should be blocked");
            var agentContext = Agent.Instance.Context;
            Agent.Instance.SetBlockingMiddlewareInstalled(true);

            // if the context is not found, skip the blocking checks, this likely means that the request is bypassed
            if (context.Items["Aikido.Zen.Context"] is not Context aikidoContext)
            {
                // call the next middleware
                await next(context);
                return;
            }

            if (context.Items["Aikido.Zen.CurrentUser"] is User user)            
                Agent.Instance.Context.AddUser(user, ipAddress: context.Connection.RemoteIpAddress?.ToString());
            

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

            if (isAllowed)
                await next(context);

            Agent.Instance.Context.AddAbortedRequest();
            context.Response.StatusCode = 429;

            if (effectiveConfig.Enabled)            
                context.Response.Headers.Add("Retry-After", effectiveConfig.WindowSizeInMS.ToString());
            

            await context.Response.WriteAsync($"You are rate limited by Aikido firewall. (Your IP: {remoteAddress})");
            return;
        }
        catch (Exception ex)
        {
            LogHelper.ErrorLog(Agent.Logger, $"Error blocking request: {ex.Message}");
            await next(context);
        }
    }
}
