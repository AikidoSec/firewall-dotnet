using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core.Sinks
{
    internal static class OutboundRequestSink
    {
        private const string OperationKind = "outgoing_http_op";

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
        internal static bool OnRequestWebRequest(WebRequest __instance, MethodBase __originalMethod)
        {
            return Inspector.Inspect(
                __originalMethod,
                OperationKind,
                context => OnRequest(__instance?.RequestUri, context));
        }

        private static InspectionResult OnRequest(Uri targetUri, Context context)
        {
            if (targetUri == null)
            {
                return InspectionResult.Allow(skipStats: true);
            }

            var hostname = targetUri.Host;
            var port = UriHelper.GetPort(targetUri);
            Agent.Instance.CaptureOutboundRequest(hostname, port);

            if (AgentHttpRequestScope.IsActive)
            {
                return InspectionResult.Allow(skipStats: true);
            }

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
    }
}
