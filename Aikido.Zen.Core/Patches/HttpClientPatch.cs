using System;
using System.Net.Http;
using System.Threading;
using Aikido.Zen.Core.Helpers;
using HarmonyLib;

namespace Aikido.Zen.Core.Patches
{
    [HarmonyPatch(typeof(HttpClient),"SendAsync", new[] { typeof(HttpRequestMessage), typeof(HttpCompletionOption), typeof(CancellationToken) })]
    internal class HttpClientPatch
    {
        [HarmonyPrefix]
        public static bool CaptureRequest(
            HttpRequestMessage request,
            CancellationToken cancellationToken,
            HttpClient __instance)
        {
            var uri = __instance.BaseAddress == null
                ? request.RequestUri
                : request.RequestUri == null
                    ? __instance.BaseAddress
                    : new Uri(__instance.BaseAddress, request.RequestUri);
                    
            var (hostname, port) = UriHelper.ExtractHost(uri);
            if (hostname.EndsWith("aikido.dev"))
                return true;
            Agent.Instance.CaptureOutboundRequest(hostname, port);
            return true;
        }
    }
}
