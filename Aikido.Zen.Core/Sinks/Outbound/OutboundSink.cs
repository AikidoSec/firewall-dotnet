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
    internal static class OutboundSink
    {
        private const string OperationKind = "outgoing_http_op";
        private const int MaxStreamUnwrapDepth = 4;

        // HttpClient flow:
        // 1. Send/SendAsync sees the target URI and current request context.
        // 2a. .NET Core stores that state on HttpRequestMessage for the resolved-address hooks.
        // 2b. .NET Framework copies that state to the created HttpWebRequest for ConnectStream.WriteHeaders.
        // 3. SendAsync finalizers make pending returned tasks surface later SSRF blocks.
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

        // WebRequest flow:
        // 1. GetResponse/GetResponseAsync sees the target URI and current request context.
        // 2a. .NET Core: WebRequest is a wrapper over an internal HttpClient request.
        //     HttpClient hooks store state on HttpRequestMessage; resolved-address hooks check SSRF.
        // 2b. .NET Framework: WebRequest sends through HttpWebRequest directly.
        //     WebRequest hooks store state on HttpWebRequest; ConnectStream.WriteHeaders checks SSRF.
        // 3. GetResponseAsync finalizers make pending returned tasks surface later SSRF blocks.
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

        // Resolved-address flow:
        // 1. Public HttpClient/WebRequest hooks store target URI and request context first.
        // 2a. .NET Core HttpConnection.SendAsync runs once the runtime has a concrete remote IP address.
        // 2b. .NET Framework ConnectStream.WriteHeaders runs once the runtime has a concrete remote IP address.
        // 3. SSRF detection compares the stored target URI with that remote IP before request bytes are sent.
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
            if (state == null || responseTask == null)
            {
                return responseTask;
            }

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
