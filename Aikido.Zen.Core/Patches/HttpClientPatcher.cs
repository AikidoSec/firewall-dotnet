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
            if (request == null || request.RequestUri == null)
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
    }
}
