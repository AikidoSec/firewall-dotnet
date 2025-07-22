using System;
using System.Net;
using System.Reflection;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Patches;
using HarmonyLib;

namespace Aikido.Zen.DotNetFramework.Patches
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

        private static void OnWebRequestFinished(WebRequest __instance, object __result, MethodBase __originalMethod)
        {
            var context = Zen.GetContext();
            if (context == null) return;

            WebResponse webResponse = null;
            // In .NET Framework, GetResponseAsync returns Task<WebResponse>.
            // Other methods like BeginGetResponse/EndGetResponse or direct GetResponse are handled.
            if (__result is System.Threading.Tasks.Task<WebResponse> taskWithWebResponse)
            {
                try
                {
                    webResponse = taskWithWebResponse.Result;
                }
                catch (AggregateException ae)
                {
                    ae.Handle(ex =>
                    {
                        LogHelper.DebugLog(Agent.Logger, $"Exception obtaining WebResponse from Task: {ex.Message}");
                        return true; // Mark as handled
                    });
                    return; // Don't proceed if there was an error getting the result
                }
            }
            else if (__result is WebResponse directWebResponse)
            {
                webResponse = directWebResponse;
            }
            // For BeginGetResponse, __result is IAsyncResult. The actual WebResponse comes from EndGetResponse.
            // Our patch on EndGetResponse should handle its result directly.
            else if (__originalMethod != null && __originalMethod.Name == "EndGetResponse" && __result is WebResponse endGetResponseResult)
            {
                // This case might be tricky because __instance for EndGetResponse is the IAsyncResult, not the WebRequest.
                // However, the more common GetResponse and GetResponseAsync are better targets for the postfix.
                // We might need a more specific way to get the original WebRequest if we solely rely on EndGetResponse.
                // For now, we rely on the other direct WebResponse returning methods.
                webResponse = endGetResponseResult;
            }

            if (webResponse != null)
            {
                // We need the original WebRequest instance here. __instance might not be it for all patched methods (e.g. EndGetResponse).
                // This patching strategy relies on __instance being the WebRequest for methods like GetResponse, GetResponseAsync.
                // If __instance is not WebRequest, this call might fail or be incorrect.
                // Consider if the __instance is the correct object for all patched methods.
                // For instance, if patching EndGetResponse(IAsyncResult), __instance is IAsyncResult.
                // A robust solution would involve storing the WebRequest instance from OnWebRequestStarted if it's not available here.
                // However, given the typical patch targets (GetResponse, GetResponseAsync), __instance is generally the WebRequest.

                var webRequestInstance = __instance;
                if (__originalMethod != null && __originalMethod.Name == "EndGetResponse" && __result is WebResponse)
                {
                    // For EndGetResponse, __instance is IAsyncResult. We need to retrieve the WebRequest from it.
                    // This requires reflection or a change in how we pass context.
                    // For simplicity, this example assumes other patches (like GetResponseAsyncInternal) will cover async scenarios
                    // where the WebRequest instance is readily available as __instance.
                    // A more complete solution for EndGetResponse would require mapping IAsyncResult back to WebRequest.
                    // This is a known complexity with Harmony patching Begin/End patterns.
                    // Let's log if this specific tricky case is hit without a WebRequest.
                    if (!(__instance is WebRequest))
                    {
                        LogHelper.DebugLog(Agent.Logger, $"OnWebRequestFinished for EndGetResponse: __instance is not WebRequest. Type: {__instance?.GetType().FullName}");
                        // Attempt to get it from the AsyncState if it was set, though this is not guaranteed.
                        if (__instance is IAsyncResult asyncResult && asyncResult.AsyncState is WebRequest stateRequest)
                        {
                            webRequestInstance = stateRequest;
                        }
                        else
                        {
                            return; // Cannot reliably get WebRequest for EndGetResponse here in this simplified model
                        }
                    }
                }

                if (webRequestInstance is WebRequest)
                {
                    WebRequestPatcher.OnWebRequestFinished(webRequestInstance, webResponse, context);
                }
                else
                {
                    LogHelper.DebugLog(Agent.Logger, $"OnWebRequestFinished: Could not determine WebRequest instance for method {__originalMethod?.Name}");
                }
            }
        }
    }
}
