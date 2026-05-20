using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Vulnerabilities;

namespace Aikido.Zen.Core.Sinks
{
    internal static class OutboundRequestSink
    {
        private const string OperationKind = "outgoing_http_op";
        private static readonly AsyncLocal<OutboundRequest> CurrentRequest = new AsyncLocal<OutboundRequest>();

        [SinkPrefix(typeof(HttpClient), "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Net.Http.HttpCompletionOption", "System.Threading.CancellationToken")]
        [SinkPrefix(typeof(HttpClient), "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken")]
        [SinkPrefix(typeof(HttpClient), "Send", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken")]
        internal static bool OnRequestHttpClient(HttpRequestMessage request, HttpClient __instance, MethodBase __originalMethod)
        {
            return Inspector.Inspect(
                __originalMethod,
                OperationKind,
                context => OnRequest(ResolveUri(request, __instance), context));
        }

        [SinkPrefix(typeof(WebRequest), "GetResponse")]
        [SinkPrefix(typeof(HttpWebRequest), "GetResponse")]
        [SinkPrefix(typeof(WebRequest), "GetResponseAsync")]
        [SinkPrefix(typeof(HttpWebRequest), "GetResponseAsync")]
        internal static bool OnRequestWebRequest(WebRequest __instance, MethodBase __originalMethod)
        {
            return Inspector.Inspect(
                __originalMethod,
                OperationKind,
                context => OnRequest(__instance?.RequestUri, context));
        }

        [SinkFinalizer]
        internal static Exception OnRequestFinalized(ref object __result, Exception __exception)
        {
            if (__result is Task<HttpResponseMessage> responseTask && CurrentRequest.Value != null)
            {
                __result = ThrowDetectedException(responseTask, CurrentRequest.Value);
            }

            ExitRequestScope();
            return __exception;
        }

        private static InspectionResult OnRequest(Uri targetUri, Context context)
        {
            if (targetUri == null)
            {
                return InspectionResult.Allow(skipStats: true);
            }

            ExitRequestScope();

            var hostname = targetUri.Host;
            var port = UriHelper.GetPort(targetUri);
            Agent.Instance.CaptureOutboundRequest(hostname, port);

            if (Agent.Instance.Context.Config.ShouldBlockOutgoingRequest(hostname))
            {
                return InspectionResult.Block(
                    AttackKind.OutboundConnectionBlocked,
                    payload: hostname,
                    metadata: new Dictionary<string, string>
                    {
                        { "hostname", hostname }
                    });
            }

            var result = SSRFDetector.Detect(targetUri, null, context, out var inspectDns);

            if (inspectDns)
            {
                EnterRequestScope(targetUri);
            }

            return result;
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

        private static void EnterRequestScope(Uri targetUri)
        {
            CurrentRequest.Value = new OutboundRequest(targetUri);
        }

        internal static void ExitRequestScope()
        {
            CurrentRequest.Value = null;
        }

        internal static bool TryGetCurrentRequestUri(out Uri targetUri)
        {
            targetUri = CurrentRequest.Value?.TargetUri;
            return targetUri != null;
        }

        internal static void SetDetectedException(AikidoException exception)
        {
            var currentRequest = CurrentRequest.Value;
            if (currentRequest != null)
            {
                currentRequest.DetectedException = exception;
            }
        }

        private static async Task<HttpResponseMessage> ThrowDetectedException(Task<HttpResponseMessage> responseTask, OutboundRequest request)
        {
            try
            {
                return await responseTask.ConfigureAwait(false);
            }
            catch (Exception) when (request.DetectedException != null)
            {
                throw request.DetectedException;
            }
        }

        private sealed class OutboundRequest
        {
            internal OutboundRequest(Uri targetUri)
            {
                TargetUri = targetUri;
            }

            internal Uri TargetUri { get; }
            internal AikidoException DetectedException { get; set; }
        }
    }
}
