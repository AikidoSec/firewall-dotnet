using System;
using System.Web;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Context = Aikido.Zen.Core.Context;
using System.Linq;
using System.Collections.Generic;

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

            if (aikidoContext == null)
            {
                throw new AikidoException("Aikido context not found in the request");
            }

            var routeKey = $"{aikidoContext.Method}|{aikidoContext.Route}";
            if (user != null)
            {
                Agent.Instance.Context.AddUser(user, aikidoContext.RemoteAddress);
            }

            // block the request if the user is blocked
            if (!EnvironmentHelper.DryMode && Agent.Instance.Context.IsBlocked(user, aikidoContext.RemoteAddress, routeKey, aikidoContext.UserAgent))
            {
                Agent.Instance.Context.AddAbortedRequest();
                throw new HttpException(403, "Request blocked");
            }

            // Check if rate limiting should be applied
            var userOrIp = user?.Id ?? aikidoContext.RemoteAddress;

            // Use the helper to check all rate limiting rules
            var (isAllowed, _) = RateLimitingHelper.IsRequestAllowed(routeKey, userOrIp, agentContext.RateLimitedRoutes);

            if (!isAllowed)
            {
                Agent.Instance.Context.AddAbortedRequest();
                throw new HttpException(429, "Too many requests");
            }
        }
    }
}
