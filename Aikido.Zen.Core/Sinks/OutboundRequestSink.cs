using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core.Sinks
{
    internal static class OutboundRequestSink
    {
        internal const string OperationKind = "outgoing_http_op";

        [SinkPrefix(typeof(HttpClient), "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Net.Http.HttpCompletionOption", "System.Threading.CancellationToken")]
        [SinkPrefix(typeof(HttpClient), "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken")]
        [SinkPrefix(typeof(HttpClient), "Send", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken")]
        internal static bool OnRequestHttpClient(HttpRequestMessage request, HttpClient __instance, MethodBase __originalMethod)
        {
            return SinkAnalyzer.Analyze(
                __originalMethod,
                OperationKind,
                context => OnRequest(ResolveUri(request, __instance), context));
        }

        [SinkPrefix(typeof(WebRequest), "GetResponse")]
        [SinkPrefix(typeof(HttpWebRequest), "GetResponse")]
        [SinkPrefix(typeof(WebRequest), "GetResponseAsync")]
        internal static bool OnRequestWebRequest(WebRequest __instance, MethodBase __originalMethod)
        {
            return SinkAnalyzer.Analyze(
                __originalMethod,
                OperationKind,
                context => OnRequest(__instance?.RequestUri, context));
        }

        internal static InspectionResult OnRequest(Uri targetUri, Context context)
        {
            if (targetUri == null)
            {
                return InspectionResult.Skip();
            }

            var hostname = targetUri.Host;
            var port = UriHelper.GetPort(targetUri);
            Agent.Instance.CaptureOutboundRequest(hostname, port);

            if (EnvironmentHelper.DryMode)
            {
                return InspectionResult.Continue();
            }

            if (Agent.Instance.Context.Config.ShouldBlockOutgoingRequest(hostname))
            {
                return InspectionResult.Block(AikidoException.OutboundConnectionBlocked(hostname));
            }

            return InspectionResult.Continue();
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
    }
}
