using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Vulnerabilities;

namespace Aikido.Zen.Core.Sinks
{
    internal static class OutboundRequestSink
    {
        private const string OperationKind = "outgoing_http_op";
        private static readonly System.Threading.AsyncLocal<RequestScope> CurrentRequest = new System.Threading.AsyncLocal<RequestScope>();

        [SinkPrefix(typeof(HttpClient), "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Net.Http.HttpCompletionOption", "System.Threading.CancellationToken")]
        [SinkPrefix(typeof(HttpClient), "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken")]
        [SinkPrefix(typeof(HttpClient), "Send", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken")]
        internal static bool OnRequestHttpClient(HttpRequestMessage request, HttpClient __instance, MethodBase __originalMethod)
        {
            return Inspector.Inspect(
                __originalMethod,
                OperationKind,
                context => OnRequest(ResolveUri(request, __instance), context, __originalMethod));
        }

        [SinkPrefix(typeof(WebRequest), "GetResponse")]
        [SinkPrefix(typeof(HttpWebRequest), "GetResponse")]
        [SinkPrefix(typeof(WebRequest), "GetResponseAsync")]
        [SinkPrefix(typeof(HttpWebRequest), "GetResponseAsync")]
        internal static bool OnRequestWebRequest(WebRequest __instance, MethodBase __originalMethod)
        {
            return Inspector.Inspect(
                __originalMethod,
                OperationKind,
                context => OnRequest(__instance?.RequestUri, context, __originalMethod));
        }

        [SinkPostfix(typeof(HttpClient), "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Net.Http.HttpCompletionOption", "System.Threading.CancellationToken")]
        [SinkPostfix(typeof(HttpClient), "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken")]
        internal static void OnHttpClientAsyncCompleted(ref Task<HttpResponseMessage> __result)
        {
            __result = ExitRequestScopeWhenCompletedAsync(__result);
        }

        [SinkPostfix(typeof(HttpClient), "Send", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken")]
        [SinkPostfix(typeof(WebRequest), "GetResponse")]
        [SinkPostfix(typeof(HttpWebRequest), "GetResponse")]
        internal static void OnRequestCompleted()
        {
            ExitRequestScope();
        }

        [SinkPostfix(typeof(WebRequest), "GetResponseAsync")]
        [SinkPostfix(typeof(HttpWebRequest), "GetResponseAsync")]
        internal static void OnWebRequestAsyncCompleted(ref Task<WebResponse> __result)
        {
            __result = ExitRequestScopeWhenCompletedAsync(__result);
        }

        [SinkFinalizer(typeof(HttpClient), "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Net.Http.HttpCompletionOption", "System.Threading.CancellationToken")]
        [SinkFinalizer(typeof(HttpClient), "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken")]
        [SinkFinalizer(typeof(HttpClient), "Send", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken")]
        [SinkFinalizer(typeof(WebRequest), "GetResponse")]
        [SinkFinalizer(typeof(HttpWebRequest), "GetResponse")]
        [SinkFinalizer(typeof(WebRequest), "GetResponseAsync")]
        [SinkFinalizer(typeof(HttpWebRequest), "GetResponseAsync")]
        internal static Exception OnRequestFinalized(Exception __exception)
        {
            if (__exception == null)
            {
                return null;
            }

            var translatedException = TryCreateDetectedAttackException() ?? __exception;
            ExitRequestScope();
            return translatedException;
        }

        [SinkPostfix(typeof(Dns), "GetHostAddresses", "System.String")]
        [SinkPostfix(typeof(Dns), "GetHostAddresses", "System.String", "System.Net.Sockets.AddressFamily")]
        internal static void OnHostAddressesResolved(IPAddress[] __result)
        {
            InspectResolvedAddresses(__result);
        }

        [SinkPostfix(typeof(Dns), "GetHostAddressesAsync", "System.String")]
        [SinkPostfix(typeof(Dns), "GetHostAddressesAsync", "System.String", "System.Threading.CancellationToken")]
        [SinkPostfix(typeof(Dns), "GetHostAddressesAsync", "System.String", "System.Net.Sockets.AddressFamily", "System.Threading.CancellationToken")]
        internal static void OnHostAddressesResolvedAsync(ref Task<IPAddress[]> __result)
        {
            __result = InspectResolvedAddressesAsync(__result);
        }

        private static InspectionResult OnRequest(Uri targetUri, Context context, MethodBase originalMethod)
        {
            if (targetUri == null)
            {
                return InspectionResult.Allow(skipStats: true);
            }

            ExitRequestScope();

            var hostname = targetUri.Host;
            var port = UriHelper.GetPort(targetUri);
            Agent.Instance.CaptureOutboundRequest(hostname, port);

            if (Agent.Instance.Context.Config.ShouldBlockOutgoingRequest(hostname))
            {
                return InspectionResult.Block(
                    AttackKind.OutboundConnectionBlocked,
                    payload: hostname,
                    metadata: new Dictionary<string, string>
                    {
                        { "hostname", hostname }
                    });
            }

            return InspectForSSRF(targetUri, context, originalMethod);
        }

        private static InspectionResult InspectForSSRF(Uri targetUri, Context context, MethodBase originalMethod)
        {
            Uri.TryCreate(context?.Url, UriKind.Absolute, out var serverUri);
            if (SSRFDetector.IsRequestToItself(serverUri, targetUri))
            {
                return InspectionResult.Allow();
            }

            if (SSRFDetector.TryGetPrivateOrLocalIPAddress(targetUri.Host, out var privateIPAddress))
            {
                return DetectSSRF(targetUri, privateIPAddress, context);
            }

            EnterRequestScope(targetUri, context, originalMethod);
            return InspectionResult.Allow();
        }

        private static InspectionResult DetectSSRF(Uri targetUri, string privateIPAddress, Context context)
        {
            if (string.IsNullOrWhiteSpace(privateIPAddress))
            {
                return InspectionResult.Allow();
            }

            var hostname = targetUri.Host;

            if (!SSRFDetector.IsRequestToServiceHostname(hostname) && context?.ParsedUserInput != null)
            {
                foreach (var userInput in context.ParsedUserInput)
                {
                    if (!Uri.TryCreate(userInput.Value, UriKind.Absolute, out var userUri) ||
                        !SSRFDetector.HasSameHostAndPort(targetUri, userUri))
                    {
                        continue;
                    }

                    var source = UserInputHelper.GetAttackSourceFromUserInputKey(userInput.Key);
                    return InspectionResult.Block(
                        AttackKind.Ssrf,
                        source: source,
                        payload: userInput.Value,
                        metadata: CreateMetadata(hostname, UriHelper.GetPort(targetUri), privateIPAddress),
                        paths: new[] { UserInputHelper.GetAttackPathFromUserInputKey(userInput.Key) });
                }
            }

            if (SSRFDetector.IsStoredSSRF(hostname, privateIPAddress))
            {
                return InspectionResult.Block(
                    AttackKind.StoredSsrf,
                    metadata: CreateMetadata(hostname, null, privateIPAddress));
            }

            return InspectionResult.Allow();
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

        private static IDictionary<string, string> CreateMetadata(string hostname, int? port, string privateIPAddress)
        {
            var metadata = new Dictionary<string, string>
            {
                { "hostname", hostname }
            };

            if (port.HasValue)
            {
                metadata["port"] = port.Value.ToString();
            }

            if (!string.IsNullOrWhiteSpace(privateIPAddress))
            {
                metadata["privateIP"] = privateIPAddress;
            }

            return metadata;
        }

        private static void EnterRequestScope(Uri targetUri, Context context, MethodBase originalMethod)
        {
            CurrentRequest.Value = new RequestScope(
                targetUri,
                context,
                ReflectionHelper.GetMethodOperation(originalMethod),
                ReflectionHelper.GetMethodModule(originalMethod));
        }

        internal static void ExitRequestScope()
        {
            CurrentRequest.Value = null;
        }

        private static async Task<T> ExitRequestScopeWhenCompletedAsync<T>(Task<T> responseTask)
            where T : class
        {
            if (responseTask == null)
            {
                ExitRequestScope();
                return null;
            }

            try
            {
                return await responseTask.ConfigureAwait(false);
            }
            catch
            {
                var exception = TryCreateDetectedAttackException();
                if (exception != null)
                {
                    throw exception;
                }

                throw;
            }
            finally
            {
                ExitRequestScope();
            }
        }

        private static async Task<IPAddress[]> InspectResolvedAddressesAsync(Task<IPAddress[]> addressesTask)
        {
            if (addressesTask == null)
            {
                return null;
            }

            var addresses = await addressesTask.ConfigureAwait(false);
            InspectResolvedAddresses(addresses);
            return addresses;
        }

        internal static void InspectResolvedAddresses(IPAddress[] addresses)
        {
            var request = CurrentRequest.Value;
            if (request == null || !TryGetPrivateOrLocalIPAddress(addresses, out var privateIPAddress))
            {
                return;
            }

            var result = DetectSSRF(request.TargetUri, privateIPAddress, request.Context);
            if (!result.AttackKind.HasValue)
            {
                return;
            }

            var blocked = !EnvironmentHelper.DryMode;

            Agent.Instance.SendAttackEvent(
                kind: result.AttackKind.Value,
                source: result.Source,
                payload: result.Payload,
                operation: request.Operation,
                context: request.Context,
                module: request.Module,
                metadata: result.Metadata,
                blocked: blocked,
                paths: result.Paths);

            if (request.Context != null)
            {
                request.Context.AttackDetected = true;
            }

            if (blocked)
            {
                request.DetectedAttackKind = result.AttackKind;
                request.DetectedAttackSource = FormatAttackSource(result);
                throw AikidoExceptionFor(request.Operation, result);
            }
        }

        private static bool TryGetPrivateOrLocalIPAddress(IPAddress[] addresses, out string privateIPAddress)
        {
            privateIPAddress = null;

            if (addresses == null)
            {
                return false;
            }

            foreach (var address in addresses)
            {
                var candidate = address?.ToString();
                if (!string.IsNullOrWhiteSpace(candidate) && IPHelper.IsPrivateOrLocalIp(candidate))
                {
                    privateIPAddress = candidate;
                    return true;
                }
            }

            return false;
        }

        private static AikidoException TryCreateDetectedAttackException()
        {
            var request = CurrentRequest.Value;
            if (request?.DetectedAttackKind == null)
            {
                return null;
            }

            return AikidoException.Blocked(
                request.DetectedAttackKind.Value,
                $"{request.Operation} originating from {request.DetectedAttackSource ?? "unknown source"}");
        }

        private static AikidoException AikidoExceptionFor(string operation, InspectionResult result)
        {
            return AikidoException.Blocked(
                result.AttackKind.Value,
                Inspector.GetBlockedOperation(operation, result));
        }

        private static string FormatAttackSource(InspectionResult result)
        {
            if (result.Source.HasValue)
            {
                var path = result.Paths.Length > 0 ? result.Paths[0] : string.Empty;
                return $"{result.Source.Value.ToJsonName()}{path}";
            }

            return "unknown source";
        }

        internal sealed class RequestScope
        {
            internal RequestScope(Uri targetUri, Context context, string operation, string module)
            {
                TargetUri = targetUri;
                Context = context;
                Operation = operation;
                Module = module;
            }

            internal Uri TargetUri { get; }
            internal Context Context { get; }
            internal string Operation { get; }
            internal string Module { get; }
            internal AttackKind? DetectedAttackKind { get; set; }
            internal string DetectedAttackSource { get; set; }
        }
    }
}
