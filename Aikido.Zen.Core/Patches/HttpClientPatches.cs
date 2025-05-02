using System;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices; // Added for ConditionalWeakTable
using Aikido.Zen.Core.Helpers;
using HarmonyLib;

namespace Aikido.Zen.Core.Patches
{
    /// <summary>
    /// Applies Harmony patches to <see cref="HttpClient"/> methods to intercept outgoing HTTP requests.
    /// </summary>
    internal static class HttpClientPatches
    {
        private const string operationKind = "outgoing_http_op";

        /// <summary>
        /// Stores state associated with an HttpRequestMessage instance between prefix and postfix patches.
        /// </summary>
        /// <remarks>
        /// We use ConditionalWeakTable keyed by HttpRequestMessage instead of HttpClient because HttpClient instances
        /// are often reused for multiple concurrent requests. Associating state with the HttpRequestMessage ensures
        /// that timing is tracked correctly for each individual request.
        /// Like in WebRequestPatches, ConditionalWeakTable provides automatic memory management via weak references
        /// to the HttpRequestMessage keys, preventing leaks if the postfix doesn't run.
        /// </remarks>
        private static readonly ConditionalWeakTable<HttpRequestMessage, RequestState> requestStates = new ConditionalWeakTable<HttpRequestMessage, RequestState>();

        /// <summary>
        /// Represents the state captured at the start of an HttpClient request operation.
        /// </summary>
        private class RequestState
        {
            public Stopwatch Stopwatch { get; }
            public string Operation { get; }
            public string Hostname { get; }
            public int Port { get; }

            public RequestState(Stopwatch stopwatch, string operation, string hostname, int port)
            {
                Stopwatch = stopwatch;
                Operation = operation;
                Hostname = hostname;
                Port = port;
            }
        }


        /// <summary>
        /// Applies patches to HttpClient methods using Harmony and reflection.
        /// </summary>
        /// <param name="harmony">The Harmony instance used for patching.</param>
        public static void ApplyPatches(Harmony harmony)
        {
            // Use reflection to get the methods dynamically
            try
            {
                // Patch SendAsync variants
                PatchMethod(harmony, "System.Net.Http", "HttpClient", "SendAsync", nameof(CaptureRequestStart), nameof(CaptureRequestEnd), "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken"); // .NET Core 2.0+ SendAsync(req, ct)
                PatchMethod(harmony, "System.Net.Http", "HttpClient", "SendAsync", nameof(CaptureRequestStart), nameof(CaptureRequestEnd), "System.Net.Http.HttpRequestMessage", "System.Net.Http.HttpCompletionOption", "System.Threading.CancellationToken"); // .NET Standard 2.0+ SendAsync(req, opt, ct)

                // Patch Send variants (synchronous) - Less common but good to cover
                PatchMethod(harmony, "System.Net.Http", "HttpClient", "Send", nameof(CaptureRequestStart), nameof(CaptureRequestEnd), "System.Net.Http.HttpRequestMessage"); // .NET 7+ Send(req)
                PatchMethod(harmony, "System.Net.Http", "HttpClient", "Send", nameof(CaptureRequestStart), nameof(CaptureRequestEnd), "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken"); // .NET 7+ Send(req, ct)
                PatchMethod(harmony, "System.Net.Http", "HttpClient", "Send", nameof(CaptureRequestStart), nameof(CaptureRequestEnd), "System.Net.Http.HttpRequestMessage", "System.Net.Http.HttpCompletionOption"); // .NET 7+ Send(req, opt)
                PatchMethod(harmony, "System.Net.Http", "HttpClient", "Send", nameof(CaptureRequestStart), nameof(CaptureRequestEnd), "System.Net.Http.HttpRequestMessage", "System.Net.Http.HttpCompletionOption", "System.Threading.CancellationToken"); // .NET 7+ Send(req, opt, ct)

            }
            catch (Exception e) // Catch broader exception type
            {
                // Consider logging the exception details here
                Console.WriteLine($"Aikido: Error patching HttpClient: {e.Message}");
            }
        }

        /// <summary>
        /// Patches a specific method on a type with prefix and postfix handlers using Harmony by dynamically retrieving it via reflection.
        /// </summary>
        /// <param name="harmony">The Harmony instance used for patching.</param>
        /// <param name="assemblyName">The name of the assembly containing the type.</param>
        /// <param name="typeName">The name of the type containing the method.</param>
        /// <param name="methodName">The name of the method to patch.</param>
        /// <param name="prefixMethodName">The name of the prefix handler method.</param>
        /// <param name="postfixMethodName">The name of the postfix handler method.</param>
        /// <param name="parameterTypeNames">The full names of the parameter types for the method overload to patch.</param>
        private static void PatchMethod(Harmony harmony, string assemblyName, string typeName, string methodName, string prefixMethodName, string postfixMethodName, params string[] parameterTypeNames)
        {
            var method = ReflectionHelper.GetMethodFromAssembly(assemblyName, typeName, methodName, parameterTypeNames);
            if (method != null && !method.IsAbstract)
            {
                var prefix = typeof(HttpClientPatches).GetMethod(prefixMethodName, BindingFlags.Static | BindingFlags.NonPublic);
                var postfix = typeof(HttpClientPatches).GetMethod(postfixMethodName, BindingFlags.Static | BindingFlags.NonPublic);
                harmony.Patch(method, prefix != null ? new HarmonyMethod(prefix) : null, postfix != null ? new HarmonyMethod(postfix) : null);
            }
            // else: Method not found or is abstract, potentially expected depending on the target framework version.
        }


        /// <summary>
        /// Harmony prefix patch executed before the original HttpClient Send/SendAsync method.
        /// Captures the start time and request details, associating them with the HttpRequestMessage.
        /// </summary>
        /// <param name="request">The HttpRequestMessage being sent.</param>
        /// <param name="__instance">The instance of HttpClient being used.</param>
        /// <param name="__originalMethod">The original method being patched.</param>
        internal static bool CaptureRequestStart(HttpRequestMessage request, HttpClient __instance, MethodBase __originalMethod)
        {
            // If request is null, we cannot track it. This might happen in edge cases or misuse of HttpClient.
            if (request == null) return true;

            try
            {
                var uri = __instance.BaseAddress == null
                    ? request.RequestUri
                    : request.RequestUri == null || !request.RequestUri.IsAbsoluteUri // Handle relative RequestUri
                        ? new Uri(__instance.BaseAddress, request.RequestUri?.OriginalString ?? string.Empty)
                        : request.RequestUri;

                // If URI is still null after trying combinations, we cannot proceed.
                if (uri == null) return true;

                var (hostname, port) = UriHelper.ExtractHost(uri);
                Agent.Instance.CaptureOutboundRequest(hostname, port); // Capture the attempt immediately

                var operation = __originalMethod?.DeclaringType?.FullName ?? __instance.GetType().FullName;
                var stopwatch = Stopwatch.StartNew();
                var state = new RequestState(stopwatch, operation, hostname, port);

                // Use Add. If the key (request) somehow already exists (e.g., misuse of HttpRequestMessage instance),
                // the Add method will throw, and the exception will be caught.
                requestStates.Add(request, state);
            }
            catch // Catch-all for safety during instrumentation
            {
                // pass through - avoid crashing the host application due to instrumentation errors
            }
            return true;
        }


        /// <summary>
        /// Harmony postfix patch executed after the original HttpClient Send/SendAsync method completes.
        /// Calculates duration and reports the inspection results using state associated with the HttpRequestMessage.
        /// </summary>
        /// <param name="request">The HttpRequestMessage that was sent. Matches the parameter name in the original methods.</param>
        internal static void CaptureRequestEnd(HttpRequestMessage request)
        {
            // If request is null, we cannot look up state.
            if (request == null) return;

            try
            {
                if (requestStates.TryGetValue(request, out var state))
                {
                    state.Stopwatch.Stop();
                    // TODO: Determine actual values once ssrf attack detection is implemented
                    bool withoutContext = true;
                    bool attackDetected = false;
                    bool blocked = false;

                    Agent.Instance.Context.OnInspectedCall(
                        state.Operation,
                        operationKind,
                        state.Stopwatch.Elapsed.TotalMilliseconds,
                        attackDetected,
                        blocked,
                        withoutContext);

                    requestStates.Remove(request); // Clean up the state for this request
                }
                // else: State not found. Could happen if prefix failed or if the request object identity changed unexpectedly.
            }
            catch // Catch-all for safety during instrumentation
            {
                // pass through - avoid crashing the host application due to instrumentation errors
            }
        }
    }
}
