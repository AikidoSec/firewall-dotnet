using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Vulnerabilities;
using HarmonyLib;

// we don't want to expose this to the consumer, yet it should be testable, hence the internal visibility and the assembly attribute
[assembly: InternalsVisibleTo("Aikido.Zen.Tests")]
namespace Aikido.Zen.Core.Patches
{
    internal static class WebRequestPatches
    {
        private const string operationKind = "outgoing_http_op";

        /// <summary>
        /// Applies patches to WebRequest methods using Harmony and reflection.
        /// </summary>
        /// <param name="harmony">The Harmony instance used for patching.</param>
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
                var patchMethod = new HarmonyMethod(typeof(WebRequestPatches).GetMethod(nameof(CaptureRequest), BindingFlags.Static | BindingFlags.NonPublic));
                harmony.Patch(method, patchMethod);
            }
        }

        /// <summary>
        /// Callback method executed before the original WebRequest method is executed.
        /// </summary>
        /// <param name="__instance">The instance of WebRequest being used.</param>
        /// <param name="__originalMethod">The original method being patched.</param>
        /// <param name="context">The current request context.</param>
        /// <returns>True if the original method should continue execution; otherwise, false.</returns>
        internal static bool CaptureRequest(WebRequest __instance, System.Reflection.MethodBase __originalMethod, Context context)
        {
            if (__instance == null || __instance.RequestUri == null)
                return true;

            var uri = __instance.RequestUri;
            var (hostname, port) = UriHelper.ExtractHost(uri);

            // Track redirects if this is a redirect request
            if (__instance is HttpWebRequest httpRequest &&
                httpRequest.Address != null &&
                httpRequest.Address != httpRequest.RequestUri)
            {
                context.OutgoingRequestRedirects.Add(new Context.RedirectInfo(
                    httpRequest.RequestUri,
                    httpRequest.Address
                ));
            }

            Agent.Instance.CaptureOutboundRequest(hostname, port);
            var methodInfo = __originalMethod as MethodInfo;
            var operation = $"{methodInfo?.DeclaringType?.Name}.{methodInfo?.Name}";

            // Check for SSRF attacks
            var stopwatch = Stopwatch.StartNew();
            var ssrfResult = SSRFDetector.CheckContextForSSRF(uri, Agent.Instance.Context, operation);
            bool attackDetected = ssrfResult != null;
            bool blocked = false;

            // If an attack is detected, we should block the request
            if (attackDetected)
            {
                blocked = true;
                // Convert metadata to IDictionary<string, object>
                var metadata = ssrfResult.Metadata.ToDictionary(
                    kvp => kvp.Key,
                    kvp => (object)kvp.Value
                );

                // Log the attack
                Agent.Instance.SendAttackEvent(
                    AttackKind.Ssrf,
                    Source.Headers,
                    ssrfResult.Payload,
                    operation,
                    context,
                    "WebRequestPatches",
                    metadata,
                    blocked
                );
            }

            Agent.Instance.Context.OnInspectedCall(
                operation,
                operationKind,
                stopwatch.ElapsedMilliseconds,
                attackDetected,
                blocked,
                context == null);

            // Return false to block the request if an attack was detected
            return !blocked;
        }
    }
}
