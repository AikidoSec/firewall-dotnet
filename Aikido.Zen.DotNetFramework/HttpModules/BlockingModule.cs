using System;
using System.Web;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Exceptions;
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
            context.PostAuthenticateRequest += Context_PostAuthenticateRequest;
        }

        private void Context_PostAuthenticateRequest(object sender, EventArgs e)
        {
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
                throw new HttpException(403, $"Your request is blocked: {HttpUtility.HtmlEncode(reason)}");
            }

            // Is rate limiting enabled for this route?
            if (agentContext.RateLimitedRoutes.TryGetValue(routeKey, out var rateLimitConfig) && rateLimitConfig.Enabled)
            {
                // should we rate limit this request?
                var key = $"{routeKey}:user-or-ip:{user?.Id ?? aikidoContext.RemoteAddress}";
                if (!RateLimitingHelper.IsAllowed(key, rateLimitConfig.WindowSizeInMS, rateLimitConfig.MaxRequests))
                {
                    Agent.Instance.Context.AddAbortedRequest();
                    throw new HttpException(429, "Too many requests");
                }
            }
        }
    }
}
