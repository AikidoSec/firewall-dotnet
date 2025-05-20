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
                var patchMethod = new HarmonyMethod(typeof(WebRequestPatches).GetMethod(nameof(OnWebRequest),
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));
                harmony.Patch(method, patchMethod);
            }
        }

        private static bool OnWebRequest(WebRequest __instance, System.Reflection.MethodBase __originalMethod)
        {
            var context = Zen.GetContext();
            return Core.Patches.WebRequestPatches.CaptureRequest(__instance, __originalMethod, context);
        }
    }
}
