using System;
using System.Net.Http;
using System.Reflection;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Patches;
using HarmonyLib;

namespace Aikido.Zen.DotNetCore.Patches
{
    internal static class HttpClientPatches
    {
        public static void ApplyPatches(Harmony harmony)
        {
            // Use reflection to get the methods dynamically
            try
            {
                // Patch the main request methods
                // PatchMethod(harmony, "System.Net.Http", "HttpClient", "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Net.Http.HttpCompletionOption", "System.Threading.CancellationToken");
                // PatchMethod(harmony, "System.Net.Http", "HttpClient", "Send", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken");

                // Patch the internal methods that handle redirects
                PatchMethod(harmony, "System.Net.Http", "HttpMessageHandlerStage", "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken");
                PatchMethod(harmony, "System.Net.Http", "HttpMessageHandlerStage", "Send", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken");
            }
            catch (NotImplementedException e)
            {
                // pass through, there may be some methods that are not implemented
                LogHelper.ErrorLog(Agent.Logger, "Error patching HttpClient:" + e.Message);
            }
        }

        private static void PatchMethod(Harmony harmony, string assemblyName, string typeName, string methodName, params string[] parameterTypeNames)
        {
            var method = ReflectionHelper.GetMethodFromAssembly(assemblyName, typeName, methodName, parameterTypeNames);
            if (method != null && !method.IsAbstract)
            {
                var onRequestStarted = new HarmonyMethod(typeof(HttpClientPatches).GetMethod(nameof(OnRequestStarted), BindingFlags.Static | BindingFlags.NonPublic));
                var onRequestFinished = new HarmonyMethod(typeof(HttpClientPatches).GetMethod(nameof(OnRequestFinished), BindingFlags.Static | BindingFlags.NonPublic));
                harmony.Patch(method, prefix: onRequestStarted, postfix: onRequestFinished);
            }
        }

        private static bool OnRequestStarted(HttpRequestMessage request, object __instance, MethodBase __originalMethod)
        {
            var context = Zen.GetContext();
            return HttpClientPatcher.OnRequestStarted(request, __originalMethod, context);
        }

        private static void OnRequestFinished(HttpRequestMessage request, object __instance, object __result)
        {
            var context = Zen.GetContext();
            
            if (__result is Task<HttpResponseMessage> asyncResponse)
                HttpClientPatcher.OnRequestFinished(request, asyncResponse.Result, context);
            if (__result is HttpResponseMessage syncResponse)
                HttpClientPatcher.OnRequestFinished(request, syncResponse, context);
        }
    }
}
