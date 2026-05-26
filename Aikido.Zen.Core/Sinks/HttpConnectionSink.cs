using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Vulnerabilities;

namespace Aikido.Zen.Core.Sinks
{
    internal static class HttpConnectionSink
    {
        private const string OperationKind = "outgoing_http_op";
        private const int MaxStreamUnwrapDepth = 4;

        [SinkPrefix("System.Net.Http", "System.Net.Http.HttpConnection", "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Boolean", "System.Threading.CancellationToken")]
        [SinkPrefix("System.Net.Http", "System.Net.Http.Http2Connection", "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Boolean", "System.Threading.CancellationToken")]
        [SinkPrefix("System.Net.Http", "System.Net.Http.Http3Connection", "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Boolean", "System.Threading.CancellationToken")]
        [SinkPrefix("System.Net.Http", "System.Net.Http.Http3Connection", "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Int64", "System.Threading.CancellationToken")]
        [SinkPrefix("System.Net.Http", "System.Net.Http.Http3Connection", "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Int64", "System.Diagnostics.Activity", "System.Threading.CancellationToken")]
        [SinkPrefix("System.Net.Http", "System.Net.Http.Http3Connection", "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Net.Http.Http3Connection+WaitForHttp3ConnectionActivity", "System.Boolean", "System.Threading.CancellationToken")]
        internal static bool OnRequest(object __instance, MethodBase __originalMethod, ref Task<HttpResponseMessage> __result)
        {
            if (!OutboundRequestSink.TryGetCurrentRequestUri(out var targetUri))
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
                    context => SSRFDetector.Detect(targetUri, remoteAddress, context));
            }
            catch (AikidoException ex)
            {
                OutboundRequestSink.SetDetectedException(ex);
                __result = Task.FromException<HttpResponseMessage>(ex);
                return false;
            }
        }

        [SinkPrefix("System", "System.Net.ConnectStream", "WriteHeaders", "System.Boolean")]
        internal static bool OnFrameworkRequest(object ___m_Connection, MethodBase __originalMethod)
        {
            if (!OutboundRequestSink.TryGetCurrentRequestUri(out var targetUri))
            {
                return true;
            }

            var remoteAddress = GetIPAddressFromStream(ReflectionHelper.GetMemberValue(___m_Connection, "NetworkStream") as Stream);
            if (remoteAddress == null)
            {
                return true;
            }

            try
            {
                return Inspector.Inspect(
                    __originalMethod,
                    OperationKind,
                    context => SSRFDetector.Detect(targetUri, remoteAddress, context));
            }
            catch (AikidoException ex)
            {
                OutboundRequestSink.SetDetectedException(ex);
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
                    return null;
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
    }
}
