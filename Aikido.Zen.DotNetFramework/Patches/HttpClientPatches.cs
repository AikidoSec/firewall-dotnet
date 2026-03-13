using System;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Patches;
using HarmonyLib;

namespace Aikido.Zen.DotNetFramework.Patches
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
                "System.Net.Http.HttpRequestMessage",
                "System.Threading.CancellationToken");

            PatchMethod(
                harmony,
                "System.Net.Http",
                "HttpClient",
                "Send",
                nameof(PrefixSend),
                nameof(PostfixSend),
                "System.Net.Http.HttpRequestMessage",
                "System.Threading.CancellationToken");
        }

        private static void PatchMethod(Harmony harmony, string assemblyName, string typeName, string methodName, string prefixMethodName, string postfixMethodName, params string[] parameterTypeNames)
        {
            var method = ReflectionHelper.GetMethodFromAssembly(assemblyName, typeName, methodName, parameterTypeNames);
            if (method == null || method.IsAbstract)
            {
                return;
            }

            var prefix = typeof(HttpClientPatches).GetMethod(prefixMethodName, BindingFlags.Static | BindingFlags.NonPublic);
            var postfix = typeof(HttpClientPatches).GetMethod(postfixMethodName, BindingFlags.Static | BindingFlags.NonPublic);
            harmony.Patch(method, new HarmonyMethod(prefix), new HarmonyMethod(postfix));
        }

        private static bool PrefixSendAsyncWithCompletionOption(HttpRequestMessage request, HttpClient __instance, MethodBase __originalMethod, ref Task<HttpResponseMessage> __result, CancellationToken cancellationToken, out Uri __state)
        {
            return InspectRequest(request, __instance, __originalMethod, ref __result, out __state);
        }

        private static void PostfixSendAsyncWithCompletionOption(MethodBase __originalMethod, ref Task<HttpResponseMessage> __result, Uri __state)
        {
            __result = InspectRedirectResponseAsync(__result, __state, GetOperation(__originalMethod), GetModule(__originalMethod));
        }

        private static bool PrefixSendAsync(HttpRequestMessage request, HttpClient __instance, MethodBase __originalMethod, ref Task<HttpResponseMessage> __result, CancellationToken cancellationToken, out Uri __state)
        {
            return InspectRequest(request, __instance, __originalMethod, ref __result, out __state);
        }

        private static void PostfixSendAsync(MethodBase __originalMethod, ref Task<HttpResponseMessage> __result, Uri __state)
        {
            __result = InspectRedirectResponseAsync(__result, __state, GetOperation(__originalMethod), GetModule(__originalMethod));
        }

        private static bool PrefixSend(HttpRequestMessage request, HttpClient __instance, MethodBase __originalMethod, ref HttpResponseMessage __result, CancellationToken cancellationToken, out Uri __state)
        {
            var targetUri = ResolveUri(request, __instance);
            __state = targetUri;
            if (targetUri == null)
            {
                return true;
            }

            return OutboundRequestPatcher.Inspect(
                targetUri,
                GetOperation(__originalMethod),
                GetModule(__originalMethod),
                Zen.GetContext());
        }

        private static void PostfixSend(HttpResponseMessage __result, MethodBase __originalMethod, Uri __state)
        {
            InspectRedirectResponse(__result?.RequestMessage?.RequestUri, __state, GetOperation(__originalMethod), GetModule(__originalMethod));
        }

        private static bool InspectRequest(HttpRequestMessage request, HttpClient client, MethodBase originalMethod, ref Task<HttpResponseMessage> result, out Uri state)
        {
            var targetUri = ResolveUri(request, client);
            state = targetUri;
            if (targetUri == null)
            {
                return true;
            }

            return OutboundRequestPatcher.Inspect(
                targetUri,
                GetOperation(originalMethod),
                GetModule(originalMethod),
                Zen.GetContext());
        }

        private static async Task<HttpResponseMessage> InspectRedirectResponseAsync(Task<HttpResponseMessage> resultTask, Uri sourceUri, string operation, string module)
        {
            var response = await resultTask.ConfigureAwait(false);
            InspectRedirectResponse(response?.RequestMessage?.RequestUri, sourceUri, operation, module);
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
