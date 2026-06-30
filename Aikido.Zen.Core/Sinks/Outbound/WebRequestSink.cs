using System;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core.Sinks
{
    internal static class WebRequestSink
    {
        private static readonly ConditionalWeakTable<HttpWebRequest, OutboundRequestState> WebRequestStates = new ConditionalWeakTable<HttpWebRequest, OutboundRequestState>();
        private static readonly object RequestStateLock = new object();

        // WebRequest flow:
        // 1. GetResponse/GetResponseAsync sees the target URI and current request context.
        // 2a. .NET Core: WebRequest is a wrapper over an internal HttpClient request.
        //     HttpClientSink stores state on HttpRequestMessage; HttpConnection.SendAsync checks SSRF.
        // 2b. .NET Framework: WebRequest sends through HttpWebRequest directly.
        //     WebRequestSink stores state on HttpWebRequest; ConnectStream.WriteHeaders checks SSRF.
        // 3. GetResponseAsync wraps the returned task so a later SSRF block is raised to the caller.

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
                TrackWebRequest(httpWebRequest, targetUri, context);
            }

            return Inspector.Inspect(
                __originalMethod,
                OutboundInnerConnectionSink.OperationKind,
                context,
                _ => OutboundInnerConnectionSink.InspectOutboundRequest(targetUri));
        }

        [SinkFinalizer(typeof(WebRequest), "GetResponseAsync")]
        [SinkFinalizer(typeof(HttpWebRequest), "GetResponseAsync")]
        internal static Exception OnWebRequestFinalized(WebRequest __instance, ref Task<WebResponse> __result, Exception __exception)
        {
            if (__result != null && TryGetRequestState(__instance as HttpWebRequest, out var state))
            {
                __result = OutboundInnerConnectionSink.ThrowDetectedException(__result, state);
            }

            return __exception;
        }

        internal static bool TryGetRequestState(HttpWebRequest request, out OutboundRequestState state)
        {
            if (request == null)
            {
                state = null;
                return false;
            }

            return WebRequestStates.TryGetValue(request, out state);
        }

        internal static void TrackAssociatedRequest(HttpWebRequest request, OutboundRequestState state)
        {
            if (request == null || state == null)
            {
                return;
            }

            SetRequestState(request, state);
        }

        private static void TrackWebRequest(HttpWebRequest request, Uri targetUri, Context context)
        {
            if (request == null || targetUri == null)
            {
                return;
            }

            SetRequestState(request, new OutboundRequestState(targetUri, context));
        }

        private static void SetRequestState(HttpWebRequest request, OutboundRequestState state)
        {
            lock (RequestStateLock)
            {
                WebRequestStates.Remove(request);
                WebRequestStates.Add(request, state);
            }
        }
    }
}
