using System;
using System.Net;
using System.Net.Http;
using System.Reflection;

namespace Aikido.Zen.Core.Sinks
{
    internal static class OutboundRequestPatches
    {
        [SinkPrefix("System.Net.Http", "HttpClient", "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Net.Http.HttpCompletionOption", "System.Threading.CancellationToken")]
        [SinkPrefix("System.Net.Http", "HttpClient", "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken")]
        [SinkPrefix("System.Net.Http", "HttpClient", "Send", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken")]
        internal static bool HttpClientRequest(HttpRequestMessage request, HttpClient __instance, MethodBase __originalMethod)
        {
            return OutboundRequestSink.OnRequest(ResolveUri(request, __instance), __originalMethod, Patcher.GetContext());
        }

        [SinkPrefix("", "System.Net.WebRequest", "GetResponse")]
        [SinkPrefix("", "System.Net.HttpWebRequest", "GetResponse")]
        [SinkPrefix("", "System.Net.WebRequest", "GetResponseAsync")]
        internal static bool WebRequest(WebRequest __instance, MethodBase __originalMethod)
        {
            return OutboundRequestSink.OnRequest(__instance?.RequestUri, __originalMethod, Patcher.GetContext());
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
