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
            var application = (HttpApplication)sender;
            HandleBlocking(application.Context, application.CompleteRequest);
        }

        internal static void HandleBlocking(HttpContext httpContext, Action completeRequest)
        {
            try
            {
                LogHelper.DebugLog(Agent.Logger, "Checking if request needs to be blocked");
                var aikidoContext = (Context)httpContext.Items["Aikido.Zen.Context"];
                var agentContext = Agent.Instance.Context;

                Agent.Instance.SetBlockingMiddlewareInstalled(true);

                // if the context is not found, skip the blocking module, this likely means that the request is bypassed
                if (aikidoContext == null)
                {
                    return;
                }

                var user = aikidoContext.User;
                if (user != null)
                {
                    Agent.Instance.Context.AddUser(user, aikidoContext.RemoteAddress);
                }

                // Attack wave detection needs to be run manually as it doesn't rely on method patching
                var attackWaveDetector = Agent.Instance.AttackWaveDetector;
                if (attackWaveDetector.Check(aikidoContext))
                {
                    var samples = attackWaveDetector.GetSamplesForIp(aikidoContext.RemoteAddress);
                    Agent.Instance.SendAttackWaveEvent(aikidoContext, samples);
                }

                // block the request if the user is blocked
                if (Agent.Instance.Context.IsBlocked(aikidoContext, out var reason))
                {
                    Agent.Instance.Context.AddAbortedRequest();
                    LogHelper.DebugLog(Agent.Logger, $"Request blocked: {HttpUtility.HtmlEncode(reason)}");
                    CompleteRequestWithResponse(
                        httpContext,
                        403,
                        $"Your request is blocked: {HttpUtility.HtmlEncode(reason)}",
                        completeRequest);
                    return;
                }

                // Use the helper to check all rate limiting rules
                var (isAllowed, effectiveConfig) = RateLimitingHelper.IsRequestAllowed(aikidoContext, agentContext.Endpoints, agentContext.Config);

                if (!isAllowed)
                {
                    Agent.Instance.Context.AddAbortedRequest();
                    CompleteRequestWithResponse(
                        httpContext,
                        429,
                        $"You are rate limited by Aikido firewall. (Your IP: {HttpUtility.HtmlEncode(aikidoContext.RemoteAddress)})",
                        completeRequest,
                        effectiveConfig?.WindowSizeInMS.ToString());
                    return;
                }
            }
            catch (Exception ex)
            {
                LogHelper.ErrorLog(Agent.Logger, $"Error blocking request: {ex.Message}");
            }
        }

        internal static void CompleteRequestWithResponse(HttpContext httpContext, int statusCode, string responseBody, Action completeRequest, string retryAfter = null)
        {
            httpContext.Response.TrySkipIisCustomErrors = true;
            httpContext.Response.StatusCode = statusCode;

            if (!string.IsNullOrEmpty(retryAfter))
            {
                httpContext.Response.AppendHeader("Retry-After", retryAfter);
            }

            if (!string.IsNullOrEmpty(responseBody))
            {
                httpContext.Response.Write(responseBody);
            }

            // CompleteRequest short-circuits pipeline nicer than Response.End (no ThreadAbortException)
            // Stored as Action for easy unit testing
            completeRequest?.Invoke();
        }
    }
}
