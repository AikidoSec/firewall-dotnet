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
        // Public outbound hooks see the supported API request objects and the current Zen context.
        // Later internal hooks may run after that context is no longer visible, so we keep the
        // captured state in global weak tables keyed by the request objects themselves.
        // HttpClient keeps state on HttpRequestMessage; WebRequest keeps state on HttpWebRequest.
        private static readonly ConditionalWeakTable<HttpRequestMessage, OutboundRequestState> HttpRequestStates = new ConditionalWeakTable<HttpRequestMessage, OutboundRequestState>();
        private static readonly ConditionalWeakTable<HttpWebRequest, OutboundRequestState> WebRequestStates = new ConditionalWeakTable<HttpWebRequest, OutboundRequestState>();

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
            try
            {
                HttpRequestStates.Add(request, state);
            }
            catch (ArgumentException)
            {
                // The request was already tracked.
            }
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
            try
            {
                WebRequestStates.Add(request, state);
            }
            catch (ArgumentException)
            {
                // The request was already tracked.
            }
        }

        internal static void AssociateHttpRequestWithWebRequest(HttpRequestMessage httpRequest, HttpWebRequest webRequest)
        {
            // .NET Framework HttpClient is backed by HttpWebRequest, so associate both keys before
            // the resolved-address check runs in ConnectStream.WriteHeaders.
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
