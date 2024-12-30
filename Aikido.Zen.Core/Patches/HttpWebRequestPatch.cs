using Aikido.Zen.Core.Helpers;
using HarmonyLib;
using System;
using System.Net;
using System.Reflection;

namespace Aikido.Zen.Core.Patches
{
    internal static class HttpWebRequestPatch
    {
        public static void ApplyPatches(Harmony harmony)
        {
            PatchMethod(harmony, typeof(HttpWebRequest), "GetResponse", nameof(CaptureRequest));
            PatchMethod(harmony, typeof(HttpWebRequest), "GetResponseAsync", nameof(CaptureRequest));
            PatchMethod(harmony, typeof(WebRequest), "GetResponse", nameof(CaptureRequest));
            PatchMethod(harmony, typeof(WebRequest), "GetResponseAsync", nameof(CaptureRequest));
        }

        private static void PatchMethod(Harmony harmony, Type type, string methodName, string patchMethodName)
        {
            var method = AccessTools.Method(type, methodName);
            if (method != null)
            {
                harmony.Patch(method, new HarmonyMethod(typeof(HttpWebRequestPatch).GetMethod(nameof(CaptureRequest), BindingFlags.Static | BindingFlags.NonPublic)));
            }
        }

        private static bool CaptureRequest(WebRequest __instance)
        {
            var (hostname, port) = UriHelper.ExtractHost(__instance.RequestUri);
            Agent.Instance.CaptureOutboundRequest(hostname, port);
            return true;
        }
    }
}
