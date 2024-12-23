using Aikido.Zen.Core.Helpers;
using HarmonyLib;
using System.Net;

namespace Aikido.Zen.Core.Patches
{
    [HarmonyPatch(typeof(HttpWebRequest), "GetResponse")]
    [HarmonyPatch(typeof(WebRequest), "GetResponseAsync")]
    public class HttpWebRequestPatch
    {
        [HarmonyPrefix]
        public static bool CaptureRequest(WebRequest __instance)
        {
            var (hostname, port) = UriHelper.ExtractHost(__instance.RequestUri);
            Agent.Instance.CaptureOutboundRequest(hostname, port);
            return true;
        }
    }
}
