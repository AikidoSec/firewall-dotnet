using System;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
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
            PatchMethod(harmony, typeof(WebRequest), "GetResponse", nameof(PrefixGetResponse), nameof(PostfixGetResponse), nameof(FinalizerGetResponse));
            PatchMethod(harmony, typeof(WebRequest), "GetResponseAsync", nameof(PrefixGetResponseAsync), nameof(PostfixGetResponseAsync), nameof(FinalizerGetResponseAsync));
            PatchMethod(harmony, typeof(HttpWebRequest), "GetResponse", nameof(PrefixGetResponse), nameof(PostfixGetResponse), nameof(FinalizerGetResponse));
            PatchMethod(harmony, typeof(HttpWebRequest), "GetResponseAsync", nameof(PrefixGetResponseAsync), nameof(PostfixGetResponseAsync), nameof(FinalizerGetResponseAsync));
        }

        private static void PatchMethod(Harmony harmony, System.Type type, string methodName, string prefixMethodName, string postfixMethodName, string finalizerMethodName)
        {
            var method = type.GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (method == null || method.IsAbstract)
            {
                return;
            }

            var prefix = typeof(WebRequestPatches).GetMethod(prefixMethodName, BindingFlags.Static | BindingFlags.NonPublic);
            var postfix = typeof(WebRequestPatches).GetMethod(postfixMethodName, BindingFlags.Static | BindingFlags.NonPublic);
            var finalizer = typeof(WebRequestPatches).GetMethod(finalizerMethodName, BindingFlags.Static | BindingFlags.NonPublic);
            harmony.Patch(method, new HarmonyMethod(prefix), new HarmonyMethod(postfix), null, new HarmonyMethod(finalizer));
        }

        private static bool PrefixGetResponse(WebRequest __instance, MethodBase __originalMethod)
        {
            return InspectRequest(__instance, __originalMethod);
        }

        private static void PostfixGetResponse()
        {
            OutboundRequestPatcher.ExitRequestScope();
        }

        private static Exception FinalizerGetResponse(Exception __exception)
        {
            if (__exception != null)
            {
                OutboundRequestPatcher.ExitRequestScope();
            }

            return __exception;
        }

        private static bool PrefixGetResponseAsync(WebRequest __instance, MethodBase __originalMethod)
        {
            return InspectRequest(__instance, __originalMethod);
        }

        private static void PostfixGetResponseAsync(ref Task<WebResponse> __result)
        {
            __result = ExitRequestScopeWhenCompletedAsync(__result);
        }

        private static Exception FinalizerGetResponseAsync(Exception __exception)
        {
            if (__exception != null)
            {
                OutboundRequestPatcher.ExitRequestScope();
            }

            return __exception;
        }

        private static bool InspectRequest(WebRequest request, MethodBase originalMethod)
        {
            if (request?.RequestUri == null)
            {
                return true;
            }

            var operation = GetOperation(originalMethod);
            var module = GetModule(originalMethod);
            OutboundRequestPatcher.Inspect(request.RequestUri, operation, module, Zen.GetContext());
            return true;
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

        private static async Task<WebResponse> ExitRequestScopeWhenCompletedAsync(Task<WebResponse> responseTask)
        {
            if (responseTask == null)
            {
                OutboundRequestPatcher.ExitRequestScope();
                return null;
            }

            try
            {
                return await responseTask.ConfigureAwait(false);
            }
            catch (Exception)
            {
                if (OutboundRequestPatcher.TryGetDetectedAttackException(out var aikidoException))
                {
                    throw aikidoException;
                }

                throw;
            }
            finally
            {
                OutboundRequestPatcher.ExitRequestScope();
            }
        }
    }
}
