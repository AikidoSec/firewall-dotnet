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

            if (aikidoContext == null)
            {
                throw new AikidoException("Aikido context not found in the request");
            }

            var routeKey = $"{aikidoContext.Method}|{aikidoContext.Route}";
            if (user != null)
            {
                Agent.Instance.Context.AddUser(user, aikidoContext.RemoteAddress);
            }

            // Is rate limiting enabled for this route?
            if (agentContext.RateLimitedRoutes.TryGetValue(routeKey, out var rateLimitConfig) && rateLimitConfig.Enabled)
            {
                // should we rate limit this request?
                if (RateLimitingHelper.IsAllowed(user?.Id ?? aikidoContext.RemoteAddress, rateLimitConfig.WindowSizeInMS, rateLimitConfig.MaxRequests))
                {
                    Agent.Instance.Context.AddAbortedRequest();
                    httpContext.Response.StatusCode = 429;
                    // stop the request from being processed
                    httpContext.Response.End();
                }
            }

            // block the request if the user is blocked
            if (Agent.Instance.Context.IsBlocked(user, aikidoContext.RemoteAddress, routeKey))
            {
                Agent.Instance.Context.AddAbortedRequest();
                httpContext.Response.StatusCode = 403;
                // stop the request from being processed
                httpContext.Response.End();
            }
        }
    }
}
