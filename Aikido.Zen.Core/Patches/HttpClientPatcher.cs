using System;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Core.Patches
{
    public static class HttpClientPatcher
    {
        private const string operationKind = "outgoing_http_op";

        public static bool OnHttpClient(HttpRequestMessage request, object instance, MethodBase originalMethod, Context context)
        {
            if (request == null || request.RequestUri == null || context == null)
                return true;

            var uri = instance is HttpClient client && client.BaseAddress != null
                ? request.RequestUri == null
                    ? client.BaseAddress
                    : new Uri(client.BaseAddress, request.RequestUri)
                : request.RequestUri;

            var methodInfo = originalMethod as MethodInfo;
            var operation = $"{methodInfo?.DeclaringType?.Name}.{methodInfo?.Name}";
            var stopwatch = Stopwatch.StartNew();
            bool withoutContext = context == null;
            bool attackDetected = false;
            bool blocked = false;

            try
            {
                context.OutgoingRequestRedirects.Add(new Context.RedirectInfo
                {
                    Destination = uri,
                    Source = request.RequestUri,
                });
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

        public static bool OnRequestFinished(HttpClient client, HttpRequestMessage request, HttpResponseMessage response, Context context)
        {
            if (client == null || request == null || response == null || context == null)
                return true;

            if (response.StatusCode == System.Net.HttpStatusCode.Redirect ||
                response.StatusCode == System.Net.HttpStatusCode.MovedPermanently ||
                response.StatusCode == System.Net.HttpStatusCode.TemporaryRedirect)
            {
                context.OutgoingRequestRedirects.Add(new Context.RedirectInfo
                {
                    Source = request.RequestUri,
                    Destination = response.Headers.Location
                });
                return !SSRFHelper.DetectSSRF(response.Headers.Location, context, "HttpClient", request.Method.ToString());
            }
            return true;
        }
    }
}
