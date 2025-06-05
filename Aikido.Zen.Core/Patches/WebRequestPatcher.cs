using System;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Core.Patches
{
    /// <summary>
    /// Provides methods to patch and monitor System.Net.WebRequest for security vulnerabilities.
    /// </summary>
    public static class WebRequestPatcher
    {
        private const string operationKind = "outgoing_http_op";

        /// <summary>
        /// Handles the event when a WebRequest is about to be sent, performs SSRF detection, and logs the operation.
        /// </summary>
        /// <param name="request">The WebRequest instance.</param>
        /// <param name="originalMethod">The original method base that was called.</param>
        /// <param name="context">The current context.</param>
        /// <returns>True if the request should proceed, throws AikidoException if an attack is detected and blocked.</returns>
        public static bool OnWebRequestStarted(WebRequest request, MethodBase originalMethod, Context context)
        {
            if (request == null || request.RequestUri == null)
                return true;

            var uri = request.RequestUri;
            var (hostname, port) = UriHelper.ExtractHost(uri);
            Agent.Instance.CaptureOutboundRequest(hostname, port);

            var methodInfo = originalMethod as MethodInfo;
            var operation = $"{methodInfo?.DeclaringType?.Name}.{methodInfo?.Name}";
            var stopwatch = Stopwatch.StartNew();
            bool withoutContext = context == null;
            bool attackDetected = false;
            bool blocked = false;

            try
            {
                attackDetected = SSRFHelper.DetectSSRF(request.RequestUri, context, "WebRequest", operation);
                blocked = attackDetected && !EnvironmentHelper.DryMode;
            }
            catch (Exception e)
            {
                LogHelper.ErrorLog(Agent.Logger, $"Error during SSRF detection: {e.Message}");
            }

            stopwatch.Stop();
            try
            {
                Agent.Instance?.Context?.OnInspectedCall(
                    operation,
                    operationKind,
                    stopwatch.Elapsed.TotalMilliseconds,
                    attackDetected,
                    blocked,
                    withoutContext);
            }
            catch (Exception e)
            {
                LogHelper.ErrorLog(Agent.Logger, $"Error recording OnInspectedCall stats: {e.Message}");
            }

            if (blocked)
            {
                throw new AikidoException($"SSRF attack detected for URI: {uri}");
            }

            return true;
        }

        /// <summary>
        /// Handles the event after a WebRequest has finished and a response has been received.
        /// This method checks for redirects and records them in the context.
        /// </summary>
        /// <param name="request">The original WebRequest instance.</param>
        /// <param name="response">The WebResponse received.</param>
        /// <param name="context">The current context.</param>
        public static void OnWebRequestFinished(WebRequest request, WebResponse response, Context context)
        {
            if (request == null || response == null || context == null)
                return;

            if (response is HttpWebResponse httpResponse)
            {
                if (httpResponse.StatusCode == HttpStatusCode.Redirect ||
                    httpResponse.StatusCode == HttpStatusCode.MovedPermanently ||
                    httpResponse.StatusCode == HttpStatusCode.TemporaryRedirect ||
                    (int)httpResponse.StatusCode == 307 ||
                    (int)httpResponse.StatusCode == 308)
                {
                    if (response.Headers["Location"] != null && Uri.TryCreate(response.Headers["Location"], UriKind.Absolute, out var locationUri))
                    {
                        context.OutgoingRequestRedirects.Add(new Context.RedirectInfo
                        {
                            Source = request.RequestUri,
                            Destination = locationUri
                        });
                    }
                }
            }
        }
    }
}
