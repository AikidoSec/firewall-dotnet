using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core.Patches
{
    internal sealed class OutboundInspectionResult
    {
        public bool ShouldProceed { get; set; } = true;
        public bool AttackDetected { get; set; }
        public bool Blocked { get; set; }
        public Exception Exception { get; set; }
    }

    internal static class OutboundRequestSink
    {
        private const string OperationKind = "outgoing_http_op";
        private static readonly Uri ParsedAikidoUrl = new Uri(EnvironmentHelper.AikidoUrl);
        private static readonly Uri ParsedAikidoRealtimeUrl = new Uri(EnvironmentHelper.AikidoRealtimeUrl);

        [PatchTarget(PatchKind.Prefix, "System.Net.Http", "HttpClient", "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Net.Http.HttpCompletionOption", "System.Threading.CancellationToken")]
        private static bool OnHttpClientSendAsyncWithCompletionOption(HttpRequestMessage request, HttpClient __instance, MethodBase __originalMethod, ref Task<HttpResponseMessage> __result, CancellationToken cancellationToken)
        {
            return InspectRequest(request, __instance, __originalMethod, ref __result);
        }

        [PatchTarget(PatchKind.Prefix, "System.Net.Http", "HttpClient", "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken")]
        private static bool OnHttpClientSendAsync(HttpRequestMessage request, HttpClient __instance, MethodBase __originalMethod, ref Task<HttpResponseMessage> __result, CancellationToken cancellationToken)
        {
            return InspectRequest(request, __instance, __originalMethod, ref __result);
        }

        [PatchTarget(PatchKind.Prefix, "System.Net.Http", "HttpClient", "Send", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken")]
        private static bool OnHttpClientSend(HttpRequestMessage request, HttpClient __instance, MethodBase __originalMethod, ref HttpResponseMessage __result, CancellationToken cancellationToken)
        {
            var targetUri = ResolveUri(request, __instance);
            if (targetUri == null)
            {
                return true;
            }

            var inspection = Inspect(
                targetUri,
                GetOperation(__originalMethod),
                GetModule(__originalMethod),
                Patcher.GetContext());

            if (inspection.ShouldProceed)
            {
                return true;
            }

            throw inspection.Exception;
        }

        [PatchTarget(PatchKind.Prefix, "", "System.Net.WebRequest", "GetResponse")]
        [PatchTarget(PatchKind.Prefix, "", "System.Net.HttpWebRequest", "GetResponse")]
        private static bool OnWebRequestGetResponse(WebRequest __instance, MethodBase __originalMethod, ref WebResponse __result)
        {
            if (__instance?.RequestUri == null)
            {
                return true;
            }

            var inspection = Inspect(
                __instance.RequestUri,
                GetOperation(__originalMethod),
                GetModule(__originalMethod),
                Patcher.GetContext());

            if (inspection.ShouldProceed)
            {
                return true;
            }

            throw inspection.Exception;
        }

        [PatchTarget(PatchKind.Prefix, "", "System.Net.WebRequest", "GetResponseAsync")]
        private static bool OnWebRequestGetResponseAsync(WebRequest __instance, MethodBase __originalMethod, ref Task<WebResponse> __result)
        {
            if (__instance?.RequestUri == null)
            {
                return true;
            }

            var inspection = Inspect(
                __instance.RequestUri,
                GetOperation(__originalMethod),
                GetModule(__originalMethod),
                Patcher.GetContext());

            if (inspection.ShouldProceed)
            {
                return true;
            }

            __result = Task.FromException<WebResponse>(inspection.Exception);
            return false;
        }

        private static bool InspectRequest(HttpRequestMessage request, HttpClient client, MethodBase originalMethod, ref Task<HttpResponseMessage> result)
        {
            var targetUri = ResolveUri(request, client);
            if (targetUri == null)
            {
                return true;
            }

            var inspection = Inspect(
                targetUri,
                GetOperation(originalMethod),
                GetModule(originalMethod),
                Patcher.GetContext());

            if (inspection.ShouldProceed)
            {
                return true;
            }

            result = Task.FromException<HttpResponseMessage>(inspection.Exception);
            return false;
        }

        internal static OutboundInspectionResult Inspect(Uri targetUri, string operation, string module, Context context)
        {
            var result = new OutboundInspectionResult();
            var withoutContext = context == null;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var hostname = targetUri.Host;
                var port = UriHelper.GetPort(targetUri);
                Agent.Instance.CaptureOutboundRequest(hostname, port);

                if (Context.IsBypassed(context))
                {
                    return result;
                }

                if (EnvironmentHelper.DryMode || IsAikidoInternalTarget(targetUri))
                {
                    return result;
                }

                if (Agent.Instance.Context.Config.ShouldBlockOutgoingRequest(hostname))
                {
                    result.ShouldProceed = false;
                    result.Blocked = true;
                    result.Exception = AikidoException.OutboundConnectionBlocked(hostname);
                    return result;
                }

                if (Agent.Instance.Context.IsProtectionDisabledForEndpoint(context))
                {
                    return result;
                }

                return result;
            }
            finally
            {
                stopwatch.Stop();
                Agent.Instance?.Context?.OnInspectedCall(
                    operation,
                    OperationKind,
                    stopwatch.Elapsed.TotalMilliseconds,
                    result.AttackDetected,
                    result.Blocked,
                    withoutContext);
            }
        }

        private static bool IsAikidoInternalTarget(Uri targetUri)
        {
            if (targetUri == null)
            {
                return false;
            }

            var targetPort = UriHelper.GetPort(targetUri);
            var targetHost = targetUri.Host;

            if (string.Equals(targetHost, ParsedAikidoUrl.Host, StringComparison.OrdinalIgnoreCase) &&
                targetPort == UriHelper.GetPort(ParsedAikidoUrl))
            {
                return true;
            }

            if (string.Equals(targetHost, ParsedAikidoRealtimeUrl.Host, StringComparison.OrdinalIgnoreCase) &&
                targetPort == UriHelper.GetPort(ParsedAikidoRealtimeUrl))
            {
                return true;
            }

            return false;
        }

        private static Uri ResolveUri(HttpRequestMessage request, HttpClient client)
        {
            if (client?.BaseAddress == null)
            {
                return request?.RequestUri;
            }

            if (request?.RequestUri == null)
            {
                return client.BaseAddress;
            }

            return new Uri(client.BaseAddress, request.RequestUri);
        }

        private static string GetOperation(MethodBase originalMethod)
        {
            var methodInfo = originalMethod as MethodInfo;
            return $"{methodInfo?.DeclaringType?.Name}.{methodInfo?.Name}";
        }

        private static string GetModule(MethodBase originalMethod)
        {
            var methodInfo = originalMethod as MethodInfo;
            return methodInfo?.DeclaringType?.Assembly.GetName().Name;
        }

    }
}
