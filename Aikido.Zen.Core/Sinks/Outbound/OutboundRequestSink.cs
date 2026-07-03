using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
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
        private const int MaxStreamUnwrapDepth = 4;

        // Outbound domain blocking uses public patches at the beginning of each supported client flow:
        // - HttpClient: Send/SendAsync prefixes see the target URI and current request context.
        //   Finalizers make pending returned tasks surface later SSRF blocks.
        // - WebRequest: GetResponse/GetResponseAsync prefixes see the target URI and current request context.
        //   Finalizers make pending returned tasks surface later SSRF blocks.
        //
        // SSRF detection uses internal patches to retrieve the final remote IP address (including any redirects):
        // - .NET Core has the resolved address in HttpConnection.SendAsync.
        // - .NET Framework has the resolved address in ConnectStream.WriteHeaders.
        // - Detection uses the stored target URI and the resolved remote IP to check for SSRF.
        //
        // Request state is carried from the outer public patches to the internal patches using ConditionalWeakTable.
        // Other storage mechanisms like AsyncLocal and HttpContext were not reliable between those patch points.
        // Chosen patches cover all combinations: .NET Core/Framework, async/sync, pooled/new requests.

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
            var isNestedRequest = OutboundRequestStateStore.TryGetHttpRequestState(request, out _);

            if (request != null && targetUri != null && !isNestedRequest)
            {
                OutboundRequestStateStore.SetHttpRequestState(request, new OutboundRequestState(targetUri, context));
            }

            return Inspector.Inspect(
                __originalMethod,
                OperationKind,
                context,
                // Nested requests can re-enter Send/SendAsync with the same HttpRequestMessage.
                // The first pass stores state and inspects the outbound host; later passes are duplicates.
                _ => isNestedRequest
                    ? InspectionResult.Allow(skipStats: true)
                    : InspectOutboundRequest(targetUri));
        }

        [SinkPostfix("System.Net.Http", "System.Net.Http.HttpClientHandler", "CreateAndPrepareWebRequest", "System.Net.Http.HttpRequestMessage")]
        internal static void OnHttpClientWebRequestCreated(HttpRequestMessage request, HttpWebRequest __result)
        {
            OutboundRequestStateStore.AssociateHttpRequestWithWebRequest(request, __result);
        }

        [SinkFinalizer(typeof(HttpClient), "SendAsync", "System.Net.Http.HttpRequestMessage")]
        [SinkFinalizer(typeof(HttpClient), "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Net.Http.HttpCompletionOption")]
        [SinkFinalizer(typeof(HttpClient), "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Net.Http.HttpCompletionOption", "System.Threading.CancellationToken")]
        [SinkFinalizer(typeof(HttpClient), "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken")]
        internal static Exception OnHttpClientRequestFinalized(HttpRequestMessage request, ref Task<HttpResponseMessage> __result, Exception __exception)
        {
            if (__result != null && OutboundRequestStateStore.TryGetHttpRequestState(request, out var state))
            {
                __result = ThrowDetectedException(__result, state);
            }

            return __exception;
        }

        [SinkPrefix(typeof(WebRequest), "GetResponse")]
        [SinkPrefix(typeof(HttpWebRequest), "GetResponse")]
        [SinkPrefix(typeof(WebRequest), "GetResponseAsync")]
        [SinkPrefix(typeof(HttpWebRequest), "GetResponseAsync")]
        internal static bool OnWebRequest(WebRequest __instance, MethodBase __originalMethod)
        {
            var context = Patcher.GetContext();
            var targetUri = __instance?.RequestUri;

            if (__instance is HttpWebRequest httpWebRequest && targetUri != null)
            {
                OutboundRequestStateStore.SetWebRequestState(httpWebRequest, new OutboundRequestState(targetUri, context));
            }

            return Inspector.Inspect(
                __originalMethod,
                OperationKind,
                context,
                _ => InspectOutboundRequest(targetUri));
        }

        [SinkFinalizer(typeof(WebRequest), "GetResponseAsync")]
        [SinkFinalizer(typeof(HttpWebRequest), "GetResponseAsync")]
        internal static Exception OnWebRequestFinalized(WebRequest __instance, ref Task<WebResponse> __result, Exception __exception)
        {
            if (__result != null && OutboundRequestStateStore.TryGetWebRequestState(__instance as HttpWebRequest, out var state))
            {
                __result = ThrowDetectedException(__result, state);
            }

            return __exception;
        }

        [SinkPrefix("System.Net.Http", "System.Net.Http.HttpConnection", "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Boolean", "System.Threading.CancellationToken")]
        [SinkPrefix("System.Net.Http", "System.Net.Http.Http2Connection", "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Boolean", "System.Threading.CancellationToken")]
        [SinkPrefix("System.Net.Http", "System.Net.Http.Http3Connection", "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Boolean", "System.Threading.CancellationToken")]
        [SinkPrefix("System.Net.Http", "System.Net.Http.Http3Connection", "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Int64", "System.Threading.CancellationToken")]
        [SinkPrefix("System.Net.Http", "System.Net.Http.Http3Connection", "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Int64", "System.Diagnostics.Activity", "System.Threading.CancellationToken")]
        [SinkPrefix("System.Net.Http", "System.Net.Http.Http3Connection", "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Net.Http.Http3Connection+WaitForHttp3ConnectionActivity", "System.Boolean", "System.Threading.CancellationToken")]
        internal static bool OnHttpClientConnectionRequest(HttpRequestMessage request, object __instance, MethodBase __originalMethod, ref Task<HttpResponseMessage> __result)
        {
            if (!OutboundRequestStateStore.TryGetHttpRequestState(request, out var state))
            {
                return true;
            }

            var remoteAddress = GetIPAddressFromConnection(__instance);
            if (remoteAddress == null)
            {
                return true;
            }

            try
            {
                return Inspector.Inspect(
                    __originalMethod,
                    OperationKind,
                    state.Context,
                    context => SSRFDetector.Detect(state.TargetUri, remoteAddress, context));
            }
            catch (AikidoException ex)
            {
                state.DetectedException = ex;
                __result = Task.FromException<HttpResponseMessage>(ex);
                return false;
            }
        }

        [SinkPrefix("System", "System.Net.ConnectStream", "WriteHeaders", "System.Boolean")]
        internal static bool OnFrameworkRequest(object __instance, object ___m_Connection, MethodBase __originalMethod)
        {
            var request = ReflectionHelper.GetMemberValue(__instance, "m_Request") as HttpWebRequest;
            if (!OutboundRequestStateStore.TryGetWebRequestState(request, out var state))
            {
                return true;
            }

            var remoteAddress = GetIPAddressFromConnection(___m_Connection);
            if (remoteAddress == null)
            {
                return true;
            }

            try
            {
                return Inspector.Inspect(
                    __originalMethod,
                    OperationKind,
                    state.Context,
                    context => SSRFDetector.Detect(state.TargetUri, remoteAddress, context));
            }
            catch (AikidoException ex)
            {
                state.DetectedException = ex;
                request?.Abort();
                throw;
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

        private static IPAddress GetIPAddressFromConnection(object connection)
        {
            switch (connection?.GetType().Name)
            {
                case "HttpConnection":
                case "Http2Connection":
                    return GetIPAddressFromStream(ReflectionHelper.GetMemberValue(connection, "_stream") as Stream);
                case "Http3Connection":
                    return GetIPAddressFromRemoteEndPoint(ReflectionHelper.GetMemberValue(connection, "_connection"));
                default:
                    return GetIPAddressFromStream(ReflectionHelper.GetMemberValue(connection, "NetworkStream") as Stream)
                        ?? GetIPAddressFromStream(ReflectionHelper.GetMemberValue(connection, "m_NetworkStream") as Stream)
                        ?? GetIPAddressFromStream(connection as Stream);
            }
        }

        private static IPAddress GetIPAddressFromStream(Stream stream)
        {
            for (var depth = 0; stream != null && depth <= MaxStreamUnwrapDepth; depth++)
            {
                switch (stream.GetType().Name)
                {
                    case "SslStream":
                        stream = ReflectionHelper.GetMemberValue(stream, "InnerStream") as Stream;
                        continue;

                    default:
                        return GetIPAddressFromRemoteEndPoint(ReflectionHelper.GetMemberValue(stream, "Socket"));
                }
            }

            return null;
        }

        private static IPAddress GetIPAddressFromRemoteEndPoint(object connection)
        {
            try
            {
                var remoteEndPoint = connection is Socket socket
                    ? socket.RemoteEndPoint
                    : ReflectionHelper.GetMemberValue(connection, "RemoteEndPoint");
                return (remoteEndPoint as IPEndPoint)?.Address;
            }
            catch
            {
                return null;
            }
        }

        // Pending async calls may only discover SSRF after the public API already returned a task.
        // The finalizer swaps that task so the recorded block still reaches the original caller.
        private static Task<TResponse> ThrowDetectedException<TResponse>(Task<TResponse> responseTask, OutboundRequestState state)
        {
            if (responseTask.IsCompleted)
            {
                if (state.DetectedException != null)
                {
                    return Task.FromException<TResponse>(state.DetectedException);
                }

                return responseTask;
            }

            return ThrowDetectedExceptionWhenCompleted(responseTask, state);
        }

        private static async Task<TResponse> ThrowDetectedExceptionWhenCompleted<TResponse>(Task<TResponse> responseTask, OutboundRequestState state)
        {
            try
            {
                return await responseTask.ConfigureAwait(false);
            }
            catch (Exception) when (state.DetectedException != null)
            {
                throw state.DetectedException;
            }
        }

        private static InspectionResult InspectOutboundRequest(Uri targetUri)
        {
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
    }
}
