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
    internal static class OutboundInnerConnectionSink
    {
        internal const string OperationKind = "outgoing_http_op";
        private const int MaxStreamUnwrapDepth = 4;

        // Common request flow:
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
            if (!HttpClientSink.TryGetRequestState(request, out var state))
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
            if (!WebRequestSink.TryGetRequestState(request, out var state))
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

        internal static Task<TResponse> ThrowDetectedException<TResponse>(Task<TResponse> responseTask, OutboundRequestState state)
        {
            if (state == null)
            {
                return responseTask;
            }

            return responseTask.ContinueWith(task =>
            {
                if (state.DetectedException != null)
                {
                    throw state.DetectedException;
                }

                return task.GetAwaiter().GetResult();
            }, TaskContinuationOptions.ExecuteSynchronously);
        }

        // Common for WebRequest and HttpClient
        internal static InspectionResult InspectOutboundRequest(Uri targetUri)
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

    internal sealed class OutboundRequestState
    {
        // Storage for context and data from the public API hook to the inner connection sink
        public OutboundRequestState(Uri targetUri, Context context)
        {
            TargetUri = targetUri;
            Context = context;
        }

        public Uri TargetUri { get; }
        public Context Context { get; }
        public AikidoException DetectedException { get; set; }
    }
}
