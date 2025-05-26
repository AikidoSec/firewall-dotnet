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
                PatchMethod(harmony, "System.Net.Http", "HttpClient", "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Net.Http.HttpCompletionOption", "System.Threading.CancellationToken");
                PatchMethod(harmony, "System.Net.Http", "HttpClient", "Send", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken");

                // Patch the internal methods that handle redirects
                PatchMethod(harmony, "System.Net.Http", "HttpClientHandler", "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken");
                PatchMethod(harmony, "System.Net.Http", "HttpClientHandler", "Send", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken");
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
                var patchMethod = new HarmonyMethod(typeof(HttpClientPatches).GetMethod(nameof(OnHttpClient), BindingFlags.Static | BindingFlags.NonPublic));
                harmony.Patch(method, patchMethod);
            }
        }

        private static bool OnHttpClient(HttpRequestMessage request, object __instance, MethodBase __originalMethod)
        {
            var context = Zen.GetContext();
            return HttpClientPatcher.OnHttpClient(request, __instance, __originalMethod, context);
        }
    }
}
