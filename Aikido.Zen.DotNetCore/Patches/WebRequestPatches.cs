using System;
using System.Net;
using System.Reflection;
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
            // Use reflection to get the methods dynamically
            try
            {
                // Patch the main request methods
                PatchMethod(harmony, "System", "WebRequest", "GetResponse");
                PatchMethod(harmony, "System", "WebRequest", "GetResponseAsync");
                PatchMethod(harmony, "System", "WebRequest", "BeginGetResponse", "System.AsyncCallback", "System.Object");
                PatchMethod(harmony, "System", "WebRequest", "EndGetResponse", "System.IAsyncResult");

                // Patch the internal methods that handle redirects
                PatchMethod(harmony, "System", "HttpWebRequest", "GetResponseInternal");
                PatchMethod(harmony, "System", "HttpWebRequest", "GetResponseAsyncInternal");
                PatchMethod(harmony, "System", "HttpWebRequest", "SubmitRequest");
            }
            catch (NotImplementedException e)
            {
                // pass through, there may be some methods that are not implemented
                LogHelper.ErrorLog(Agent.Logger, "Error patching WebRequest:" + e.Message);
            }
        }

        private static void PatchMethod(Harmony harmony, string assemblyName, string typeName, string methodName, params string[] parameterTypeNames)
        {
            var method = ReflectionHelper.GetMethodFromAssembly(assemblyName, typeName, methodName, parameterTypeNames);
            if (method != null && !method.IsAbstract)
            {
                var onStarted = new HarmonyMethod(typeof(WebRequestPatches).GetMethod(nameof(OnWebRequestStarted), BindingFlags.Static | BindingFlags.NonPublic));
                var onFinished = new HarmonyMethod(typeof(WebRequestPatches).GetMethod(nameof(OnWebRequestFinished), BindingFlags.Static | BindingFlags.NonPublic));
                harmony.Patch(method, prefix: onStarted, postfix: onFinished);
            }
        }

        private static bool OnWebRequestStarted(WebRequest __instance, MethodBase __originalMethod)
        {
            var context = Zen.GetContext();
            return WebRequestPatcher.OnWebRequestStarted(__instance, __originalMethod, context);
        }

        private static void OnWebRequestFinished(WebRequest __instance, object __result)
        {
            var context = Zen.GetContext();
            if (context == null) return;

            WebResponse webResponse = null;
            if (__result is System.Threading.Tasks.Task<WebResponse> taskWithWebResponse)
            {
                webResponse = taskWithWebResponse.Result;
            }
            else if (__result is WebResponse directWebResponse)
            {
                webResponse = directWebResponse;
            }
            // For BeginGetResponse/EndGetResponse pattern, __result of BeginGetResponse is IAsyncResult
            // and EndGetResponse is where the actual WebResponse is obtained. We'll handle EndGetResponse separately if needed,
            // but often the other patched methods like GetResponseAsyncInternal cover these paths.

            if (webResponse != null)
            {
                WebRequestPatcher.OnWebRequestFinished(__instance, webResponse, context);
            }
        }
    }
}
