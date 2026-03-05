using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Patches;
using HarmonyLib;

namespace Aikido.Zen.DotNetFramework.Patches
{
    internal static class WebRequestPatches
    {
        public static void ApplyPatches(Harmony harmony)
        {
            PatchMethod(harmony, typeof(WebRequest), "GetResponse", nameof(PrefixGetResponse));
            PatchMethod(harmony, typeof(WebRequest), "GetResponseAsync", nameof(PrefixGetResponseAsync));
            PatchMethod(harmony, typeof(HttpWebRequest), "GetResponse", nameof(PrefixGetResponse));
            PatchMethod(harmony, typeof(HttpWebRequest), "GetResponseAsync", nameof(PrefixGetResponseAsync));
        }

        private static void PatchMethod(Harmony harmony, System.Type type, string methodName, string prefixMethodName)
        {
            try
            {
                var method = AccessTools.Method(type, methodName);
                if (method == null || method.IsAbstract)
                {
                    return;
                }

                var prefix = typeof(WebRequestPatches).GetMethod(prefixMethodName, BindingFlags.Static | BindingFlags.NonPublic);
                harmony.Patch(method, new HarmonyMethod(prefix));
            }
            catch
            {
                // Ignore missing or non-patchable methods across framework versions.
            }
        }

        private static bool PrefixGetResponse(WebRequest __instance, MethodBase __originalMethod, ref WebResponse __result)
        {
            if (__instance?.RequestUri == null)
            {
                return true;
            }

            var inspection = OutboundRequestPatcher.Inspect(
                __instance.RequestUri,
                GetOperation(__originalMethod),
                GetModule(__originalMethod),
                Zen.GetContext());

            if (inspection.ShouldProceed)
            {
                return true;
            }

            throw inspection.Exception;
        }

        private static bool PrefixGetResponseAsync(WebRequest __instance, MethodBase __originalMethod, ref Task<WebResponse> __result)
        {
            if (__instance?.RequestUri == null)
            {
                return true;
            }

            var inspection = OutboundRequestPatcher.Inspect(
                __instance.RequestUri,
                GetOperation(__originalMethod),
                GetModule(__originalMethod),
                Zen.GetContext());

            if (inspection.ShouldProceed)
            {
                return true;
            }

            __result = Task.FromException<WebResponse>(inspection.Exception);
            return false;
        }

        private static string GetOperation(MethodBase originalMethod)
        {
            var methodInfo = originalMethod as MethodInfo;
            return $"{methodInfo?.DeclaringType?.Name}.{methodInfo?.Name}";
        }

        private static string GetModule(MethodBase originalMethod)
        {
            var methodInfo = originalMethod as MethodInfo;
            return methodInfo?.DeclaringType?.Assembly.GetName().Name;
        }
    }
}
