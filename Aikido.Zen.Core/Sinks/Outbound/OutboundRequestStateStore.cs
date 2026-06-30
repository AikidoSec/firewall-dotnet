using System;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core.Sinks
{
    internal static class OutboundRequestStateStore
    {
        private static readonly ConditionalWeakTable<HttpRequestMessage, OutboundRequestState> HttpRequestStates = new ConditionalWeakTable<HttpRequestMessage, OutboundRequestState>();
        private static readonly ConditionalWeakTable<HttpWebRequest, OutboundRequestState> WebRequestStates = new ConditionalWeakTable<HttpWebRequest, OutboundRequestState>();
        private static readonly object WebRequestStateLock = new object();

        internal static bool TryGetHttpRequestState(HttpRequestMessage request, out OutboundRequestState state)
        {
            if (request == null)
            {
                state = null;
                return false;
            }

            return HttpRequestStates.TryGetValue(request, out state);
        }

        internal static void SetHttpRequestState(HttpRequestMessage request, OutboundRequestState state)
        {
            HttpRequestStates.Add(request, state);
        }

        internal static bool TryGetWebRequestState(HttpWebRequest request, out OutboundRequestState state)
        {
            if (request == null)
            {
                state = null;
                return false;
            }

            return WebRequestStates.TryGetValue(request, out state);
        }

        internal static void SetWebRequestState(HttpWebRequest request, OutboundRequestState state)
        {
            lock (WebRequestStateLock)
            {
                WebRequestStates.Remove(request);
                WebRequestStates.Add(request, state);
            }
        }

        internal static void AssociateHttpRequestWithWebRequest(HttpRequestMessage httpRequest, HttpWebRequest webRequest)
        {
            if (webRequest == null || !TryGetHttpRequestState(httpRequest, out var state))
            {
                return;
            }

            SetWebRequestState(webRequest, state);
        }
    }

    internal sealed class OutboundRequestState
    {
        // Carries data from the public outbound API hook to the inner connection hook.
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
