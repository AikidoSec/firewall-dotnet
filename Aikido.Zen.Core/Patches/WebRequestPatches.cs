using System;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using Aikido.Zen.Core.Helpers;
using HarmonyLib;

// we don't want to expose this to the consumer, yet it should be testable, hence the internal visibility and the assembly attribute
[assembly: InternalsVisibleTo("Aikido.Zen.Tests")]
namespace Aikido.Zen.Core.Patches
{
    /// <summary>
    /// Applies Harmony patches to <see cref="WebRequest"/> and related classes to intercept outgoing HTTP requests.
    /// </summary>
    internal static class WebRequestPatches
    {
        private const string operationKind = "outgoing_http_op";

        /// <summary>
        /// Stores state associated with a WebRequest instance between prefix and postfix patches.
        /// </summary>
        /// <remarks>
        /// We use ConditionalWeakTable instead of a ConcurrentDictionary for several reasons:
        /// 1. Automatic Memory Management: ConditionalWeakTable holds *weak* references to the keys (WebRequest instances).
        ///    This means that if a WebRequest object is garbage collected, its corresponding entry in the table
        ///    is automatically removed. This prevents memory leaks even if the postfix patch (which calls Remove)
        ///    fails to execute for some reason.
        /// 2. Simplicity: We can use the WebRequest instance itself as the key directly, avoiding the need to generate
        ///    and manage separate request IDs that would be required with a ConcurrentDictionary.
        /// Although ConcurrentDictionary offers thread-safe operations, ConditionalWeakTable is also thread-safe
        /// for its core operations (Add, TryGetValue, Remove) and is better suited for associating transient state
        /// with object instances that have their own lifecycle.
        /// </remarks>
        private static readonly ConditionalWeakTable<WebRequest, RequestState> requestStates = new ConditionalWeakTable<WebRequest, RequestState>();

        /// <summary>
        /// Represents the state captured at the start of a WebRequest operation.
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
        /// Applies the necessary patches using the provided Harmony instance.
        /// </summary>
        /// <param name="harmony">The Harmony instance.</param>
        public static void ApplyPatches(Harmony harmony)
        {
            PatchMethod(harmony, typeof(WebRequest), "GetResponse", nameof(CaptureRequestStart), nameof(CaptureRequestEnd));
            PatchMethod(harmony, typeof(WebRequest), "GetResponseAsync", nameof(CaptureRequestStart), nameof(CaptureRequestEnd));
            PatchMethod(harmony, typeof(HttpWebRequest), "GetResponse", nameof(CaptureRequestStart), nameof(CaptureRequestEnd));
            PatchMethod(harmony, typeof(HttpWebRequest), "GetResponseAsync", nameof(CaptureRequestStart), nameof(CaptureRequestEnd));
        }

        /// <summary>
        /// Patches a specific method on a type with prefix and postfix handlers.
        /// </summary>
        /// <param name="harmony">The Harmony instance.</param>
        /// <param name="type">The type containing the method to patch.</param>
        /// <param name="methodName">The name of the method to patch.</param>
        /// <param name="prefixMethodName">The name of the prefix handler method.</param>
        /// <param name="postfixMethodName">The name of the postfix handler method.</param>
        private static void PatchMethod(Harmony harmony, Type type, string methodName, string prefixMethodName, string postfixMethodName)
        {
            try
            {
                var method = AccessTools.Method(type, methodName);
                if (method != null && !method.IsAbstract)
                {
                    var prefix = typeof(WebRequestPatches).GetMethod(prefixMethodName, BindingFlags.Static | BindingFlags.NonPublic);
                    var postfix = typeof(WebRequestPatches).GetMethod(postfixMethodName, BindingFlags.Static | BindingFlags.NonPublic);
                    harmony.Patch(method, prefix != null ? new HarmonyMethod(prefix) : null, postfix != null ? new HarmonyMethod(postfix) : null);
                }
            }
            catch
            {
                // pass through
            }
        }

        /// <summary>
        /// Harmony prefix patch executed before the original WebRequest method.
        /// Captures the start time and request details.
        /// </summary>
        /// <param name="__instance">The WebRequest instance.</param>
        /// <param name="__originalMethod">The original method being patched.</param>
        internal static bool CaptureRequestStart(WebRequest __instance, MethodBase __originalMethod)
        {
            try
            {
                var (hostname, port) = UriHelper.ExtractHost(__instance.RequestUri);
                Agent.Instance.CaptureOutboundRequest(hostname, port); // Keep capturing the attempt immediately
                var operation = __originalMethod?.DeclaringType?.FullName ?? __instance.GetType().FullName;
                var stopwatch = Stopwatch.StartNew();
                var state = new RequestState(stopwatch, operation, hostname, port);
                // Use Add. If the key somehow already exists (unlikely), the Add method will throw,
                // and the exception will be caught by the outer try-catch block.
                requestStates.Add(__instance, state);
            }
            catch
            {
                // pass through
            }
            return true;

        }

        /// <summary>
        /// Harmony postfix patch executed after the original WebRequest method completes.
        /// Calculates duration and reports the inspection results.
        /// </summary>
        /// <param name="__instance">The WebRequest instance.</param>
        internal static void CaptureRequestEnd(WebRequest __instance)
        {
            try
            {
                if (requestStates.TryGetValue(__instance, out var state))
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

                    requestStates.Remove(__instance); // Clean up the state
                }
            }
            catch
            {
                // pass through
            }
        }
    }
}
