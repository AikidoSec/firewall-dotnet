using System;
using System.Web;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Context = Aikido.Zen.Core.Context;

namespace Aikido.Zen.DotNetFramework.HttpModules
{
    /// <summary>
    /// This Http module is used to block incoming requests based on the firewall's configuration
    /// </summary>
    internal class BlockingModule : IHttpModule
    {
        public void Dispose()
        {
            // Nothing to dispose
        }

        public void Init(HttpApplication context)
        {
            LogHelper.DebugLog(Agent.Logger, "BlockingModule initialized");
            context.PostAuthenticateRequest += Context_PostAuthenticateRequest;
        }

        private void Context_PostAuthenticateRequest(object sender, EventArgs e)
        {
            try
            {
                LogHelper.DebugLog(Agent.Logger, "Checking if request needs to be blocked");
                var httpContext = ((HttpApplication)sender).Context;
                var user = (User)httpContext.Items["Aikido.Zen.CurrentUser"];
                var aikidoContext = (Context)httpContext.Items["Aikido.Zen.Context"];
                var agentContext = Agent.Instance.Context;

                Agent.Instance.SetBlockingMiddlewareInstalled(true);

                // if the context is not found, skip the blocking module, this likely means that the request is bypassed
                if (aikidoContext == null)
                {
                    return;
                }

                var routeKey = $"{aikidoContext.Method}|{aikidoContext.Route}";
                if (user != null)
                {
                    Agent.Instance.Context.AddUser(user, aikidoContext.RemoteAddress);
                }

                // block the request if the user is blocked
                if (Agent.Instance.Context.IsBlocked(aikidoContext, out var reason))
                {
                    Agent.Instance.Context.AddAbortedRequest();
                    LogHelper.DebugLog(Agent.Logger, $"Request blocked: {HttpUtility.HtmlEncode(reason)}");
                    throw new HttpException(403, $"Your request is blocked: {HttpUtility.HtmlEncode(reason)}");
                }

                // Use the helper to check all rate limiting rules
                var (isAllowed, effectiveConfig) = RateLimitingHelper.IsRequestAllowed(aikidoContext, agentContext.Endpoints);

                if (!isAllowed)
                {
                    Agent.Instance.Context.AddAbortedRequest();
                    httpContext.Response.StatusCode = 429;

                    if (effectiveConfig != null)
                    {
                        httpContext.Response.Headers.Add("Retry-After", effectiveConfig.WindowSizeInMS.ToString());
                    }

                    httpContext.Response.Write($"You are rate limited by Aikido firewall. (Your IP: {aikidoContext.RemoteAddress})");
                    httpContext.Response.End();
                    return;
                }
            }
            catch (Exception ex)
            {
                LogHelper.ErrorLog(Agent.Logger, $"Error blocking request: {ex.Message}");
            }
        }
    }
}
