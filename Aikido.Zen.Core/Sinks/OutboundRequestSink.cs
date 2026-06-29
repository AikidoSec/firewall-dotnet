using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core.Sinks
{
    internal static class OutboundRequestSink
    {
        private const string OperationKind = "outgoing_http_op";
        // .NET Framework writes headers after HttpContext.Current is no longer available,
        // so keep the request context attached to the outbound request object.
        private static readonly ConditionalWeakTable<object, OutboundRequestState> Requests = new ConditionalWeakTable<object, OutboundRequestState>();
        private static readonly object RequestLock = new object();

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
            var targetUri = ResolveUri(request, __instance);
            TrackRequest(request, targetUri);
            return Inspector.Inspect(
                __originalMethod,
                OperationKind,
                context => OnRequest(targetUri, context));
        }

        [SinkPostfix("System.Net.Http", "System.Net.Http.HttpClientHandler", "CreateAndPrepareWebRequest", "System.Net.Http.HttpRequestMessage")]
        internal static void OnHttpClientWebRequestCreated(HttpRequestMessage request, HttpWebRequest __result)
        {
            AssociateRequest(request, __result);
        }

        [SinkFinalizer(typeof(HttpClient), "SendAsync", "System.Net.Http.HttpRequestMessage")]
        [SinkFinalizer(typeof(HttpClient), "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Net.Http.HttpCompletionOption")]
        [SinkFinalizer(typeof(HttpClient), "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Net.Http.HttpCompletionOption", "System.Threading.CancellationToken")]
        [SinkFinalizer(typeof(HttpClient), "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken")]
        internal static Exception OnHttpClientRequestFinalized(HttpRequestMessage request, ref Task<HttpResponseMessage> __result, Exception __exception)
        {
            if (__result != null && TryGetTrackedRequest(request, out var state))
            {
                __result = ThrowDetectedException(__result, state);
            }

            return __exception;
        }

        [SinkPrefix(typeof(WebRequest), "GetResponse")]
        [SinkPrefix(typeof(HttpWebRequest), "GetResponse")]
        [SinkPrefix(typeof(WebRequest), "GetResponseAsync")]
        [SinkPrefix(typeof(HttpWebRequest), "GetResponseAsync")]
        internal static bool OnRequestWebRequest(WebRequest __instance, MethodBase __originalMethod)
        {
            TrackRequest(__instance, __instance?.RequestUri);
            return Inspector.Inspect(
                __originalMethod,
                OperationKind,
                context => OnRequest(__instance?.RequestUri, context));
        }

        [SinkFinalizer(typeof(WebRequest), "GetResponseAsync")]
        [SinkFinalizer(typeof(HttpWebRequest), "GetResponseAsync")]
        internal static Exception OnWebRequestFinalized(WebRequest __instance, ref Task<WebResponse> __result, Exception __exception)
        {
            if (__result != null && TryGetTrackedRequest(__instance, out var state))
            {
                __result = ThrowDetectedException(__result, state);
            }

            return __exception;
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

        internal static bool TryGetTrackedRequest(object request, out OutboundRequestState state)
        {
            state = null;
            return request != null && Requests.TryGetValue(request, out state);
        }

        private static void TrackRequest(object request, Uri targetUri)
        {
            if (request == null || targetUri == null)
            {
                return;
            }

            var state = new OutboundRequestState(targetUri, Patcher.GetContext());
            lock (RequestLock)
            {
                Requests.Remove(request);
                Requests.Add(request, state);
            }
        }

        private static void AssociateRequest(object sourceRequest, object targetRequest)
        {
            if (sourceRequest == null || targetRequest == null)
            {
                return;
            }

            if (!Requests.TryGetValue(sourceRequest, out var state))
            {
                return;
            }

            lock (RequestLock)
            {
                Requests.Remove(targetRequest);
                Requests.Add(targetRequest, state);
            }
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
