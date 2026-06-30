using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core.Sinks
{
    internal static class HttpClientSink
    {
        private static readonly ConditionalWeakTable<HttpRequestMessage, OutboundRequestState> HttpRequestStates = new ConditionalWeakTable<HttpRequestMessage, OutboundRequestState>();
        private static readonly object RequestStateLock = new object();

        // HttpClient flow:
        // 1. Send/SendAsync sees the target URI and current request context.
        // 2a. .NET Core stores that state on HttpRequestMessage for HttpConnection.SendAsync.
        // 2b. .NET Framework copies that state to the created HttpWebRequest for ConnectStream.WriteHeaders.
        // 3. SendAsync wraps the returned task so a later SSRF block is raised to the caller.

        [SinkPrefix(typeof(HttpClient), "SendAsync", "System.Net.Http.HttpRequestMessage")]
        [SinkPrefix(typeof(HttpClient), "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Net.Http.HttpCompletionOption")]
        [SinkPrefix(typeof(HttpClient), "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Net.Http.HttpCompletionOption", "System.Threading.CancellationToken")]
        [SinkPrefix(typeof(HttpClient), "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken")]
        [SinkPrefix(typeof(HttpClient), "Send", "System.Net.Http.HttpRequestMessage")]
        [SinkPrefix(typeof(HttpClient), "Send", "System.Net.Http.HttpRequestMessage", "System.Net.Http.HttpCompletionOption")]
        [SinkPrefix(typeof(HttpClient), "Send", "System.Net.Http.HttpRequestMessage", "System.Net.Http.HttpCompletionOption", "System.Threading.CancellationToken")]
        [SinkPrefix(typeof(HttpClient), "Send", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken")]
        internal static bool OnHttpClientRequest(HttpRequestMessage request, HttpClient __instance, MethodBase __originalMethod)
        {
            var context = Patcher.GetContext();
            var targetUri = ResolveUri(request, __instance);

            if (targetUri != null)
            {
                TrackHttpClientRequest(request, targetUri, context);
            }

            return Inspector.Inspect(
                __originalMethod,
                OutboundInnerConnectionSink.OperationKind,
                context,
                _ => OutboundInnerConnectionSink.InspectOutboundRequest(targetUri));
        }

        [SinkPostfix("System.Net.Http", "System.Net.Http.HttpClientHandler", "CreateAndPrepareWebRequest", "System.Net.Http.HttpRequestMessage")]
        internal static void OnHttpClientWebRequestCreated(HttpRequestMessage request, HttpWebRequest __result)
        {
            AssociateHttpClientRequestWithWebRequest(request, __result);
        }

        [SinkFinalizer(typeof(HttpClient), "SendAsync", "System.Net.Http.HttpRequestMessage")]
        [SinkFinalizer(typeof(HttpClient), "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Net.Http.HttpCompletionOption")]
        [SinkFinalizer(typeof(HttpClient), "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Net.Http.HttpCompletionOption", "System.Threading.CancellationToken")]
        [SinkFinalizer(typeof(HttpClient), "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken")]
        internal static Exception OnHttpClientRequestFinalized(HttpRequestMessage request, ref Task<HttpResponseMessage> __result, Exception __exception)
        {
            if (__result != null && TryGetRequestState(request, out var state))
            {
                __result = OutboundInnerConnectionSink.ThrowDetectedException(__result, state);
            }

            return __exception;
        }

        internal static bool TryGetRequestState(HttpRequestMessage request, out OutboundRequestState state)
        {
            if (request == null)
            {
                state = null;
                return false;
            }

            return HttpRequestStates.TryGetValue(request, out state);
        }

        private static void TrackHttpClientRequest(HttpRequestMessage request, Uri targetUri, Context context)
        {
            if (request == null || targetUri == null)
            {
                return;
            }

            SetRequestState(request, new OutboundRequestState(targetUri, context));
        }

        private static void AssociateHttpClientRequestWithWebRequest(HttpRequestMessage httpRequest, HttpWebRequest webRequest)
        {
            if (webRequest == null || !TryGetRequestState(httpRequest, out var state))
            {
                return;
            }

            WebRequestSink.TrackAssociatedRequest(webRequest, state);
        }

        private static void SetRequestState(HttpRequestMessage request, OutboundRequestState state)
        {
            lock (RequestStateLock)
            {
                HttpRequestStates.Remove(request);
                HttpRequestStates.Add(request, state);
            }
        }

        private static Uri ResolveUri(HttpRequestMessage request, HttpClient client)
        {
            var requestUri = request?.RequestUri;
            var baseAddress = client?.BaseAddress;
            if (requestUri == null)
            {
                return baseAddress;
            }

            if (requestUri.IsAbsoluteUri)
            {
                return requestUri;
            }

            return baseAddress == null ? null : new Uri(baseAddress, requestUri);
        }
    }
}
