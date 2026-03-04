using System;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Aikido.Zen.Core.Helpers;
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
                "System.Net.Http.HttpRequestMessage",
                "System.Net.Http.HttpCompletionOption",
                "System.Threading.CancellationToken");

            PatchMethod(
                harmony,
                "System.Net.Http",
                "HttpClient",
                "SendAsync",
                nameof(PrefixSendAsync),
                "System.Net.Http.HttpRequestMessage",
                "System.Threading.CancellationToken");

            PatchMethod(
                harmony,
                "System.Net.Http",
                "HttpClient",
                "Send",
                nameof(PrefixSend),
                "System.Net.Http.HttpRequestMessage",
                "System.Threading.CancellationToken");
        }

        private static void PatchMethod(Harmony harmony, string assemblyName, string typeName, string methodName, string prefixMethodName, params string[] parameterTypeNames)
        {
            var method = ReflectionHelper.GetMethodFromAssembly(assemblyName, typeName, methodName, parameterTypeNames);
            if (method == null || method.IsAbstract)
            {
                return;
            }

            var prefix = typeof(HttpClientPatches).GetMethod(prefixMethodName, BindingFlags.Static | BindingFlags.NonPublic);
            harmony.Patch(method, new HarmonyMethod(prefix));
        }

        private static bool PrefixSendAsyncWithCompletionOption(HttpRequestMessage request, HttpClient __instance, MethodBase __originalMethod, ref Task<HttpResponseMessage> __result, CancellationToken cancellationToken)
        {
            return InspectRequest(request, __instance, __originalMethod, ref __result);
        }

        private static bool PrefixSendAsync(HttpRequestMessage request, HttpClient __instance, MethodBase __originalMethod, ref Task<HttpResponseMessage> __result, CancellationToken cancellationToken)
        {
            return InspectRequest(request, __instance, __originalMethod, ref __result);
        }

        private static bool PrefixSend(HttpRequestMessage request, HttpClient __instance, MethodBase __originalMethod, ref HttpResponseMessage __result, CancellationToken cancellationToken)
        {
            var targetUri = ResolveUri(request, __instance);
            if (targetUri == null)
            {
                return true;
            }

            var inspection = OutboundRequestHelper.Inspect(
                targetUri,
                GetOperation(__originalMethod),
                GetModule(__originalMethod),
                Zen.GetContext());

            if (inspection.ShouldProceed)
            {
                return true;
            }

            throw inspection.Exception;
        }

        private static bool InspectRequest(HttpRequestMessage request, HttpClient client, MethodBase originalMethod, ref Task<HttpResponseMessage> result)
        {
            var targetUri = ResolveUri(request, client);
            if (targetUri == null)
            {
                return true;
            }

            var inspection = OutboundRequestHelper.Inspect(
                targetUri,
                GetOperation(originalMethod),
                GetModule(originalMethod),
                Zen.GetContext());

            if (inspection.ShouldProceed)
            {
                return true;
            }

            result = Task.FromException<HttpResponseMessage>(inspection.Exception);
            return false;
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
