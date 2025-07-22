using System;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Microsoft.AspNetCore.Http;

namespace Aikido.Zen.Core.Patches
{
    public static class HttpClientPatcher
    {
        private const string operationKind = "outgoing_http_op";

        public static bool OnRequestStarted(HttpRequestMessage request, MethodBase originalMethod, Context context)
        {
            if (request == null || request.RequestUri == null || context == null)
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
                attackDetected = SSRFHelper.DetectSSRF(uri, context, "HttpClient", operation);
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

        public static void OnRequestFinished(HttpRequestMessage request, HttpResponseMessage response, Context context)
        {
            if (request == null || response == null || context == null)
                return;

            if (response.StatusCode == System.Net.HttpStatusCode.Redirect ||
                response.StatusCode == System.Net.HttpStatusCode.MovedPermanently ||
                response.StatusCode == System.Net.HttpStatusCode.TemporaryRedirect ||
                (int)response.StatusCode == 307 ||
                (int)response.StatusCode == 308)
            {
                // Add the redirect info to the context so we can follow redirect chains to check for ssrf against the final destination
                if (response.Headers.Location != null && !string.IsNullOrEmpty(response.Headers.Location.Host))
                {
                    context.OutgoingRequestRedirects.Add(new Context.RedirectInfo
                    {
                        Source = request.RequestUri,
                        Destination = response.Headers.Location
                    });
                }
            }
        }
    }
}
