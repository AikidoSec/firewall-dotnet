using System;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using Aikido.Zen.Core.Helpers;
using HarmonyLib;

// we don't want to expose this to the consumer, yet it should be testable, hence the internal visibility and the assembly attribute
[assembly: InternalsVisibleTo("Aikido.Zen.Tests")]
namespace Aikido.Zen.Core.Patches
{
    internal static class WebRequestPatches
    {
        private const string kind = "outgoing_http_op";
        public static void ApplyPatches(Harmony harmony)
        {
            PatchMethod(harmony, typeof(WebRequest), "GetResponse", nameof(CaptureRequest));
            PatchMethod(harmony, typeof(WebRequest), "GetResponseAsync", nameof(CaptureRequest));
            PatchMethod(harmony, typeof(HttpWebRequest), "GetResponse", nameof(CaptureRequest));
            PatchMethod(harmony, typeof(HttpWebRequest), "GetResponseAsync", nameof(CaptureRequest));
        }

        private static void PatchMethod(Harmony harmony, Type type, string methodName, string patchMethodName)
        {
            try
            {
                var method = AccessTools.Method(type, methodName);
                if (method != null && !method.IsAbstract)
                {
                    harmony.Patch(method, new HarmonyMethod(typeof(WebRequestPatches).GetMethod(nameof(CaptureRequest), BindingFlags.Static | BindingFlags.NonPublic)));
                }
            }
            catch
            {
                // some methods fail to patch (abstract or not implemented) depending on the framework, so we just ignore them
            }

        }

        internal static bool CaptureRequest(WebRequest __instance, System.Reflection.MethodBase __originalMethod)
        {
            var (hostname, port) = UriHelper.ExtractHost(__instance.RequestUri);
            Agent.Instance.CaptureOutboundRequest(hostname, port);
            var operation = __originalMethod?.DeclaringType.FullName ?? __instance.GetType().FullName;
            var stopwatch = Stopwatch.StartNew();
            bool withoutContext = true;
            bool attackDetected = false;
            bool blocked = false;
            Agent.Instance.Context.OnInspectedCall(operation, kind, stopwatch.Elapsed.TotalMilliseconds, attackDetected, blocked, withoutContext);
            return true;
        }
    }
}
