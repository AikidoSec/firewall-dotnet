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

namespace Aikido.Zen.Core.Sinks
{
    internal static class OutboundRequestSink
    {
        private const string OperationKind = "outgoing_http_op";
        private static readonly AsyncLocal<OutboundRequestState> CurrentRequest = new AsyncLocal<OutboundRequestState>();

        [SinkPrefix(typeof(HttpClient), "SendAsync", "System.Net.Http.HttpRequestMessage")]
        [SinkPrefix(typeof(HttpClient), "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Net.Http.HttpCompletionOption")]
        [SinkPrefix(typeof(HttpClient), "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Net.Http.HttpCompletionOption", "System.Threading.CancellationToken")]
        [SinkPrefix(typeof(HttpClient), "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken")]
        [SinkPrefix(typeof(HttpClient), "Send", "System.Net.Http.HttpRequestMessage")]
        [SinkPrefix(typeof(HttpClient), "Send", "System.Net.Http.HttpRequestMessage", "System.Net.Http.HttpCompletionOption")]
        [SinkPrefix(typeof(HttpClient), "Send", "System.Net.Http.HttpRequestMessage", "System.Net.Http.HttpCompletionOption", "System.Threading.CancellationToken")]
        [SinkPrefix(typeof(HttpClient), "Send", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken")]
        internal static bool OnRequestHttpClient(HttpRequestMessage request, HttpClient __instance, MethodBase __originalMethod)
        {
            var context = Patcher.GetContext();
            var targetUri = ResolveUri(request, __instance);
            var isNestedRequest = TryGetCurrentRequest(out _);
            if (!isNestedRequest)
            {
                // Inner connection hooks still need context when Inspector skips this outer call.
                EnterRequestScope(targetUri, context);
            }

            return Inspector.Inspect(
                __originalMethod,
                OperationKind,
                context,
                _ => OnRequest(targetUri, isNestedRequest));
        }

        [SinkPrefix(typeof(WebRequest), "GetResponse")]
        [SinkPrefix(typeof(HttpWebRequest), "GetResponse")]
        [SinkPrefix(typeof(WebRequest), "GetResponseAsync")]
        [SinkPrefix(typeof(HttpWebRequest), "GetResponseAsync")]
        internal static bool OnRequestWebRequest(WebRequest __instance, MethodBase __originalMethod)
        {
            var context = Patcher.GetContext();
            var targetUri = __instance?.RequestUri;
            var isNestedRequest = TryGetCurrentRequest(out _);
            if (!isNestedRequest)
            {
                // Inner connection hooks still need context when Inspector skips this outer call.
                EnterRequestScope(targetUri, context);
            }

            return Inspector.Inspect(
                __originalMethod,
                OperationKind,
                context,
                _ => OnRequest(targetUri, isNestedRequest));
        }

        [SinkFinalizer]
        internal static Exception OnRequestFinalized(ref object __result, Exception __exception)
        {
            if (__result is Task<HttpResponseMessage> httpResponseTask && CurrentRequest.Value != null)
            {
                __result = ThrowDetectedException(httpResponseTask, CurrentRequest.Value);
            }
            else if (__result is Task<WebResponse> webResponseTask && CurrentRequest.Value != null)
            {
                __result = ThrowDetectedException(webResponseTask, CurrentRequest.Value);
            }

            ExitRequestScope();
            return __exception;
        }

        private static InspectionResult OnRequest(Uri targetUri, bool isNestedRequest)
        {
            // Modern WebRequest wraps HttpClient, so the inner HttpClient hook
            // should not replace the original request URI or report stats twice.
            if (isNestedRequest)
            {
                return InspectionResult.Allow(skipStats: true);
            }

            if (targetUri == null)
            {
                return InspectionResult.Allow(skipStats: true);
            }

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

            return InspectionResult.Allow();
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

        internal static bool TryGetCurrentRequest(out OutboundRequestState state)
        {
            state = CurrentRequest.Value;
            return state != null;
        }

        internal static bool TryGetCurrentRequestUri(out Uri targetUri)
        {
            targetUri = CurrentRequest.Value?.TargetUri;
            return targetUri != null;
        }

        private static void EnterRequestScope(Uri targetUri, Context context)
        {
            ExitRequestScope();

            if (targetUri == null)
            {
                return;
            }

            var state = new OutboundRequestState(targetUri, context);
            CurrentRequest.Value = state;
        }

        internal static void ExitRequestScope()
        {
            CurrentRequest.Value = null;
        }

        private static async Task<T> ThrowDetectedException<T>(Task<T> responseTask, OutboundRequestState state)
        {
            try
            {
                return await responseTask.ConfigureAwait(false);
            }
            catch (Exception) when (state?.DetectedException != null)
            {
                throw state.DetectedException;
            }
        }

        internal sealed class OutboundRequestState
        {
            internal OutboundRequestState(Uri targetUri, Context context)
            {
                TargetUri = targetUri;
                Context = context;
            }

            internal Uri TargetUri { get; }
            internal Context Context { get; }
            internal AikidoException DetectedException { get; set; }
        }
    }
}
