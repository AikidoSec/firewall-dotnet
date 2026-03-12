using System;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Patches;
using HarmonyLib;

namespace Aikido.Zen.DotNetCore.Patches
{
    internal static class WebRequestPatches
    {
        public static void ApplyPatches(Harmony harmony)
        {
            PatchMethod(harmony, typeof(WebRequest), "GetResponse", nameof(PrefixGetResponse), nameof(PostfixGetResponse));
            PatchMethod(harmony, typeof(WebRequest), "GetResponseAsync", nameof(PrefixGetResponseAsync), nameof(PostfixGetResponseAsync));
            PatchMethod(harmony, typeof(HttpWebRequest), "GetResponse", nameof(PrefixGetResponse), nameof(PostfixGetResponse));
            PatchMethod(harmony, typeof(HttpWebRequest), "GetResponseAsync", nameof(PrefixGetResponseAsync), nameof(PostfixGetResponseAsync));
        }

        private static void PatchMethod(Harmony harmony, System.Type type, string methodName, string prefixMethodName, string postfixMethodName)
        {
            var method = AccessTools.Method(type, methodName);
            if (method == null || method.IsAbstract)
            {
                return;
            }

            var prefix = typeof(WebRequestPatches).GetMethod(prefixMethodName, BindingFlags.Static | BindingFlags.NonPublic);
            var postfix = typeof(WebRequestPatches).GetMethod(postfixMethodName, BindingFlags.Static | BindingFlags.NonPublic);
            harmony.Patch(method, new HarmonyMethod(prefix), new HarmonyMethod(postfix));
        }

        private static bool PrefixGetResponse(WebRequest __instance, MethodBase __originalMethod, ref WebResponse __result, out Uri __state)
        {
            __state = __instance?.RequestUri;
            if (__instance?.RequestUri == null)
            {
                return true;
            }

            return OutboundRequestPatcher.Inspect(
                __instance.RequestUri,
                GetOperation(__originalMethod),
                GetModule(__originalMethod),
                Zen.GetContext());
        }

        private static void PostfixGetResponse(WebResponse __result, MethodBase __originalMethod, Uri __state)
        {
            InspectRedirectResponse(__result?.ResponseUri, __state, GetOperation(__originalMethod), GetModule(__originalMethod));
        }

        private static bool PrefixGetResponseAsync(WebRequest __instance, MethodBase __originalMethod, ref Task<WebResponse> __result, out Uri __state)
        {
            __state = __instance?.RequestUri;
            if (__instance?.RequestUri == null)
            {
                return true;
            }

            return OutboundRequestPatcher.Inspect(
                __instance.RequestUri,
                GetOperation(__originalMethod),
                GetModule(__originalMethod),
                Zen.GetContext());
        }

        private static void PostfixGetResponseAsync(MethodBase __originalMethod, ref Task<WebResponse> __result, Uri __state)
        {
            __result = InspectRedirectResponseAsync(__result, __state, GetOperation(__originalMethod), GetModule(__originalMethod));
        }

        private static async Task<WebResponse> InspectRedirectResponseAsync(Task<WebResponse> resultTask, Uri sourceUri, string operation, string module)
        {
            var response = await resultTask.ConfigureAwait(false);
            InspectRedirectResponse(response?.ResponseUri, sourceUri, operation, module);
            return response;
        }

        private static void InspectRedirectResponse(Uri destinationUri, Uri sourceUri, string operation, string module)
        {
            if (!WasRedirected(sourceUri, destinationUri))
            {
                return;
            }

            var context = Zen.GetContext();
            if (context == null)
            {
                return;
            }

            context.OutgoingRequestRedirects.Add(new Context.RedirectInfo(sourceUri, destinationUri));
            OutboundRequestPatcher.Inspect(destinationUri, operation, module, context);
        }

        private static bool WasRedirected(Uri sourceUri, Uri destinationUri)
        {
            if (sourceUri == null || destinationUri == null)
            {
                return false;
            }

            return Uri.Compare(sourceUri, destinationUri, UriComponents.HttpRequestUrl, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) != 0;
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
