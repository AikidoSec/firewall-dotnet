using System;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using Aikido.Zen.Core.Helpers;
using HarmonyLib;

namespace Aikido.Zen.Core.Patches
{
    internal static class HttpClientPatches
    {
        private const string operationKind = "outgoing_http_op";
        /// <summary>
        /// Applies patches to HttpClient methods using Harmony and reflection.
        /// </summary>
        /// <param name="harmony">The Harmony instance used for patching.</param>
        public static void ApplyPatches(Harmony harmony)
        {
            // Use reflection to get the methods dynamically
            try
            {
                PatchMethod(harmony, "System.Net.Http", "HttpClient", "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Net.Http.HttpCompletionOption", "System.Threading.CancellationToken");
                PatchMethod(harmony, "System.Net.Http", "HttpClient", "Send", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken");
            }
            catch (NotImplementedException e)
            {
                // pass through, there may be some methods that are not implemented
                Console.WriteLine("Aikido: error patching HttpClient:" + e.Message);
            }

        }

        /// <summary>
        /// Patches a method using Harmony by dynamically retrieving it via reflection.
        /// </summary>
        /// <param name="harmony">The Harmony instance used for patching.</param>
        /// <param name="assemblyName">The name of the assembly containing the type.</param>
        /// <param name="typeName">The name of the type containing the method.</param>
        /// <param name="methodName">The name of the method to patch.</param>
        /// <param name="parameterTypeNames">The names of the parameter types for the method.</param>
        private static void PatchMethod(Harmony harmony, string assemblyName, string typeName, string methodName, params string[] parameterTypeNames)
        {
            var method = ReflectionHelper.GetMethodFromAssembly(assemblyName, typeName, methodName, parameterTypeNames);
            if (method != null && !method.IsAbstract)
            {
                var patchMethod = new HarmonyMethod(typeof(HttpClientPatches).GetMethod(nameof(CaptureRequest), BindingFlags.Static | BindingFlags.NonPublic));
                harmony.Patch(method, patchMethod);
            }
        }

        /// <summary>
        /// Callback method executed before the original HttpClient method is executed.
        /// </summary>
        /// <param name="request">The HttpRequestMessage being sent.</param>
        /// <param name="__instance">The instance of HttpClient being used.</param>
        /// <returns>True if the original method should continue execution; otherwise, false.</returns>
        internal static bool CaptureRequest(HttpRequestMessage request, HttpClient __instance, System.Reflection.MethodBase __originalMethod)
        {
            var uri = __instance.BaseAddress == null
                ? request.RequestUri
                : request.RequestUri == null
                    ? __instance.BaseAddress
                    : new Uri(__instance.BaseAddress, request.RequestUri);

            var (hostname, port) = UriHelper.ExtractHost(uri);
            Agent.Instance.CaptureOutboundRequest(hostname, port);
            var methodInfo = __originalMethod as MethodInfo;
            var operation = $"{methodInfo?.DeclaringType?.Name}.{methodInfo?.Name}";
            bool withoutContext = true;
            bool attackDetected = false;
            bool blocked = false;
            Agent.Instance.Context.OnInspectedCall(
                       operation,
                       operationKind,
                       0, // once ssrf attack detection is implemented, we can measure the algorithm's performance
                       attackDetected,
                       blocked,
                       withoutContext);
            return true;
        }
    }
}
