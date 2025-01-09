using Aikido.Zen.Core.Helpers;
using HarmonyLib;
using System;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;

// we don't want to expose this to the consumer, yet it should be testable, hence the internal visibility and the assembly attribute
[assembly: InternalsVisibleTo("Aikido.Zen.Tests")]
namespace Aikido.Zen.Core.Patches
{
    internal static class WebRequestPatches
    {
        public static void ApplyPatches(Harmony harmony)
        {
            PatchMethod(harmony, typeof(WebRequest), "GetResponse", nameof(CaptureRequest));
            PatchMethod(harmony, typeof(WebRequest), "GetResponseAsync", nameof(CaptureRequest));
        }

        private static void PatchMethod(Harmony harmony, Type type, string methodName, string patchMethodName)
        {
            var method = AccessTools.Method(type, methodName);
            if (method != null)
            {
                harmony.Patch(method, new HarmonyMethod(typeof(WebRequestPatches).GetMethod(nameof(CaptureRequest), BindingFlags.Static | BindingFlags.NonPublic)));
            }
        }

        internal static bool CaptureRequest(WebRequest __instance)
        {
            var (hostname, port) = UriHelper.ExtractHost(__instance.RequestUri);
            Agent.Instance.CaptureOutboundRequest(hostname, port);
            return true;
        }
    }
}
