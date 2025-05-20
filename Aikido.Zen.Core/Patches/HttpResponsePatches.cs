using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using Aikido.Zen.Core.Helpers;
using HarmonyLib;

namespace Aikido.Zen.Core.Patches
{
    internal static class HttpResponsePatches
    {
        private const string operationKind = "outgoing_http_op";

        /// <summary>
        /// Applies patches to HTTP response methods using Harmony and reflection.
        /// </summary>
        /// <param name="harmony">The Harmony instance used for patching.</param>
        public static void ApplyPatches(Harmony harmony)
        {
            // Use reflection to get the methods dynamically
            try
            {
                // Patch WebResponse methods
                PatchMethod(harmony, "System", "WebResponse", "GetResponseStream");
                PatchMethod(harmony, "System", "WebResponse", "GetResponseStreamAsync");

                // Patch HttpResponseMessage methods
                PatchMethod(harmony, "System.Net.Http", "HttpResponseMessage", "EnsureSuccessStatusCode");
            }
            catch (NotImplementedException e)
            {
                // pass through, there may be some methods that are not implemented
                LogHelper.ErrorLog(Agent.Logger, "Error patching HttpResponse:" + e.Message);
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
                var patchMethod = new HarmonyMethod(typeof(HttpResponsePatches).GetMethod(nameof(CaptureResponse), BindingFlags.Static | BindingFlags.NonPublic));
                harmony.Patch(method, patchMethod);
            }
        }

        /// <summary>
        /// Callback method executed before the original response method is executed.
        /// </summary>
        /// <param name="__instance">The instance of the response being used.</param>
        /// <param name="__originalMethod">The original method being patched.</param>
        /// <returns>True if the original method should continue execution; otherwise, false.</returns>
        internal static bool CaptureResponse(object __instance, System.Reflection.MethodBase __originalMethod)
        {
            if (__instance == null)
                return true;

            // Handle WebResponse
            if (__instance is WebResponse webResponse)
            {
                if (webResponse is HttpWebResponse httpResponse)
                {
                    // Check if this is a redirect response
                    if (IsRedirectStatusCode(httpResponse.StatusCode))
                    {
                        var location = httpResponse.Headers["Location"];
                        if (!string.IsNullOrEmpty(location))
                        {
                            var redirectUri = new Uri(httpResponse.ResponseUri, location);
                            Agent.Instance.Context.OutgoingRequestRedirects.Add(new Context.RedirectInfo(
                                httpResponse.ResponseUri,
                                redirectUri
                            ));
                        }
                    }
                }
            }
            // Handle HttpResponseMessage
            else if (__instance is HttpResponseMessage httpResponse)
            {
                // Check if this is a redirect response
                if (IsRedirectStatusCode(httpResponse.StatusCode))
                {
                    var location = httpResponse.Headers.Location;
                    if (location != null)
                    {
                        var redirectUri = new Uri(httpResponse.RequestMessage.RequestUri, location);
                        Agent.Instance.Context.OutgoingRequestRedirects.Add(new Context.RedirectInfo(
                            httpResponse.RequestMessage.RequestUri,
                            redirectUri
                        ));
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Checks if a status code indicates a redirect.
        /// </summary>
        /// <param name="statusCode">The HTTP status code to check.</param>
        /// <returns>True if the status code indicates a redirect; otherwise, false.</returns>
        private static bool IsRedirectStatusCode(HttpStatusCode statusCode)
        {
            return statusCode == HttpStatusCode.Moved ||
                   statusCode == HttpStatusCode.Found ||
                   statusCode == HttpStatusCode.SeeOther ||
                   statusCode == HttpStatusCode.TemporaryRedirect;
        }
    }
}
