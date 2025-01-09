using System;
using System.Net.Http;
using System.Threading;
using System.Reflection;
using Aikido.Zen.Core.Helpers;
using HarmonyLib;

namespace Aikido.Zen.Core.Patches
{
    internal static class HttpClientPatches
    {
        public static void ApplyPatches(Harmony harmony)
        {
            var method = AccessTools.Method(typeof(HttpClient), "SendAsync", new[] { 
                typeof(HttpRequestMessage), 
                typeof(HttpCompletionOption), 
                typeof(CancellationToken) 
            });

            if (method != null)
            {
                harmony.Patch(method, new HarmonyMethod(typeof(HttpClientPatches).GetMethod(nameof(CaptureRequest), BindingFlags.Static | BindingFlags.NonPublic)));
            }
        }

        internal static bool CaptureRequest(
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
