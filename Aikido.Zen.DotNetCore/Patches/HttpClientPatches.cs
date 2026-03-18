using System;
using System.Net.Http;
using System.Reflection;
using System.Threading;
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

        private static bool PrefixSendAsyncWithCompletionOption(HttpRequestMessage request, HttpClient __instance, MethodBase __originalMethod, CancellationToken cancellationToken, out OutboundRequestHelper.RequestScopeState __state)
        {
            return InspectRequest(request, __instance, __originalMethod, out __state);
        }

        private static void PostfixSendAsyncWithCompletionOption(OutboundRequestHelper.RequestScopeState __state)
        {
            OutboundRequestHelper.ExitRequestScope(__state);
        }

        private static Exception FinalizerSendAsyncWithCompletionOption(Exception __exception, OutboundRequestHelper.RequestScopeState __state)
        {
            if (__exception != null)
            {
                OutboundRequestHelper.ExitRequestScope(__state);
            }

            return __exception;
        }

        private static bool PrefixSendAsync(HttpRequestMessage request, HttpClient __instance, MethodBase __originalMethod, CancellationToken cancellationToken, out OutboundRequestHelper.RequestScopeState __state)
        {
            return InspectRequest(request, __instance, __originalMethod, out __state);
        }

        private static void PostfixSendAsync(OutboundRequestHelper.RequestScopeState __state)
        {
            OutboundRequestHelper.ExitRequestScope(__state);
        }

        private static Exception FinalizerSendAsync(Exception __exception, OutboundRequestHelper.RequestScopeState __state)
        {
            if (__exception != null)
            {
                OutboundRequestHelper.ExitRequestScope(__state);
            }

            return __exception;
        }

        private static bool PrefixSend(HttpRequestMessage request, HttpClient __instance, MethodBase __originalMethod, CancellationToken cancellationToken, out OutboundRequestHelper.RequestScopeState __state)
        {
            return InspectRequest(request, __instance, __originalMethod, out __state);
        }

        private static void PostfixSend(OutboundRequestHelper.RequestScopeState __state)
        {
            OutboundRequestHelper.ExitRequestScope(__state);
        }

        private static Exception FinalizerSend(Exception __exception, OutboundRequestHelper.RequestScopeState __state)
        {
            if (__exception != null)
            {
                OutboundRequestHelper.ExitRequestScope(__state);
            }

            return __exception;
        }

        private static bool InspectRequest(HttpRequestMessage request, HttpClient client, MethodBase originalMethod, out OutboundRequestHelper.RequestScopeState state)
        {
            state = null;
            var targetUri = ResolveUri(request, client);
            if (targetUri == null)
            {
                return true;
            }

            var operation = GetOperation(originalMethod);
            var module = GetModule(originalMethod);
            if (!OutboundRequestPatcher.Inspect(targetUri, operation, module, Zen.GetContext()))
            {
                return false;
            }

            state = OutboundRequestHelper.EnterRequestScope(targetUri, operation, module);
            return true;
        }

        private static Uri ResolveUri(HttpRequestMessage request, HttpClient client)
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
