using System;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
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
            PatchMethod(
                harmony,
                "System.Net.Http",
                "HttpClient",
                "SendAsync",
                nameof(PrefixSendAsyncWithCompletionOption),
                nameof(PostfixSendAsyncWithCompletionOption),
                nameof(FinalizerSendAsyncWithCompletionOption),
                "System.Net.Http.HttpRequestMessage",
                "System.Net.Http.HttpCompletionOption",
                "System.Threading.CancellationToken");

            PatchMethod(
                harmony,
                "System.Net.Http",
                "HttpClient",
                "SendAsync",
                nameof(PrefixSendAsync),
                nameof(PostfixSendAsync),
                nameof(FinalizerSendAsync),
                "System.Net.Http.HttpRequestMessage",
                "System.Threading.CancellationToken");

            PatchMethod(
                harmony,
                "System.Net.Http",
                "HttpClient",
                "Send",
                nameof(PrefixSend),
                nameof(PostfixSend),
                nameof(FinalizerSend),
                "System.Net.Http.HttpRequestMessage",
                "System.Threading.CancellationToken");
        }

        private static void PatchMethod(Harmony harmony, string assemblyName, string typeName, string methodName, string prefixMethodName, string postfixMethodName, string finalizerMethodName, params string[] parameterTypeNames)
        {
            var method = ReflectionHelper.GetMethodFromAssembly(assemblyName, typeName, methodName, parameterTypeNames);
            if (method == null || method.IsAbstract)
            {
                return;
            }

            var prefix = typeof(HttpClientPatches).GetMethod(prefixMethodName, BindingFlags.Static | BindingFlags.NonPublic);
            var postfix = typeof(HttpClientPatches).GetMethod(postfixMethodName, BindingFlags.Static | BindingFlags.NonPublic);
            var finalizer = typeof(HttpClientPatches).GetMethod(finalizerMethodName, BindingFlags.Static | BindingFlags.NonPublic);
            harmony.Patch(method, new HarmonyMethod(prefix), new HarmonyMethod(postfix), null, new HarmonyMethod(finalizer));
        }

        private static bool PrefixSendAsyncWithCompletionOption(HttpRequestMessage request, HttpClient __instance, MethodBase __originalMethod, CancellationToken cancellationToken)
        {
            return InspectRequest(request, __instance, __originalMethod);
        }

        private static void PostfixSendAsyncWithCompletionOption(ref Task<HttpResponseMessage> __result)
        {
            __result = ExitRequestScopeWhenCompletedAsync(__result);
        }

        private static Exception FinalizerSendAsyncWithCompletionOption(Exception __exception)
        {
            if (__exception != null)
            {
                OutboundRequestPatcher.ExitRequestScope();
            }

            return __exception;
        }

        private static bool PrefixSendAsync(HttpRequestMessage request, HttpClient __instance, MethodBase __originalMethod, CancellationToken cancellationToken)
        {
            return InspectRequest(request, __instance, __originalMethod);
        }

        private static void PostfixSendAsync(ref Task<HttpResponseMessage> __result)
        {
            __result = ExitRequestScopeWhenCompletedAsync(__result);
        }

        private static Exception FinalizerSendAsync(Exception __exception)
        {
            if (__exception != null)
            {
                OutboundRequestPatcher.ExitRequestScope();
            }

            return __exception;
        }

        private static bool PrefixSend(HttpRequestMessage request, HttpClient __instance, MethodBase __originalMethod, CancellationToken cancellationToken)
        {
            return InspectRequest(request, __instance, __originalMethod);
        }

        private static void PostfixSend()
        {
            OutboundRequestPatcher.ExitRequestScope();
        }

        private static Exception FinalizerSend(Exception __exception)
        {
            if (__exception != null)
            {
                OutboundRequestPatcher.ExitRequestScope();
            }

            return __exception;
        }

        private static bool InspectRequest(HttpRequestMessage request, HttpClient client, MethodBase originalMethod)
        {
            var targetUri = ExtractUri(request, client);
            if (targetUri == null)
            {
                return true;
            }

            var methodInfo = originalMethod as MethodInfo;
            var operation = $"{methodInfo?.DeclaringType?.Name}.{methodInfo?.Name}";
            var module = methodInfo?.DeclaringType?.Assembly.GetName().Name;

            OutboundRequestPatcher.Inspect(targetUri, operation, module, Zen.GetContext());
            return true;
        }

        private static Uri ExtractUri(HttpRequestMessage request, HttpClient client)
        {
            if (client?.BaseAddress == null)
            {
                return request?.RequestUri;
            }

            if (request?.RequestUri == null)
            {
                return client.BaseAddress;
            }

            return new Uri(client.BaseAddress, request.RequestUri);
        }

        private static async Task<HttpResponseMessage> ExitRequestScopeWhenCompletedAsync(Task<HttpResponseMessage> responseTask)
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
