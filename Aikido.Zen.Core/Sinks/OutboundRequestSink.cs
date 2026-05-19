using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Vulnerabilities;

namespace Aikido.Zen.Core.Sinks
{
    internal static class OutboundRequestSink
    {
        private const string OperationKind = "outgoing_http_op";
        private static readonly System.Threading.AsyncLocal<bool> IsRequesting = new System.Threading.AsyncLocal<bool>();

        [SinkPrefix(typeof(HttpClient), "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Net.Http.HttpCompletionOption", "System.Threading.CancellationToken")]
        [SinkPrefix(typeof(HttpClient), "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken")]
        [SinkPrefix(typeof(HttpClient), "Send", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken")]
        internal static bool OnRequestHttpClient(HttpRequestMessage request, HttpClient __instance, MethodBase __originalMethod)
        {
            return Inspector.Inspect(
                __originalMethod,
                OperationKind,
                context => OnRequest(ResolveUri(request, __instance), context));
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
                context => OnRequest(__instance?.RequestUri, context));
        }

        [SinkFinalizer]
        internal static Exception OnRequestFinalized(Exception __exception)
        {
            ExitRequestScope();
            return __exception;
        }

        private static InspectionResult OnRequest(Uri targetUri, Context context)
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

            return InspectForSSRF(targetUri, context);
        }

        private static InspectionResult InspectForSSRF(Uri targetUri, Context context)
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

            EnterRequestScope();
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

        private static void EnterRequestScope()
        {
            IsRequesting.Value = true;
        }

        internal static void ExitRequestScope()
        {
            IsRequesting.Value = false;
        }

        internal static bool IsRequestingOutbound()
        {
            return IsRequesting.Value;
        }

        internal static InspectionResult DetectResolvedSSRF(string hostname, string privateIPAddress, Context context)
        {
            if (string.IsNullOrWhiteSpace(privateIPAddress))
            {
                return InspectionResult.Allow();
            }

            if (!SSRFDetector.IsRequestToServiceHostname(hostname) && context?.ParsedUserInput != null)
            {
                foreach (var userInput in context.ParsedUserInput)
                {
                    if (!Uri.TryCreate(userInput.Value, UriKind.Absolute, out var userUri) ||
                        !HasSameHost(hostname, userUri))
                    {
                        continue;
                    }

                    var source = UserInputHelper.GetAttackSourceFromUserInputKey(userInput.Key);
                    return InspectionResult.Block(
                        AttackKind.Ssrf,
                        source: source,
                        payload: userInput.Value,
                        metadata: CreateMetadata(hostname, null, privateIPAddress),
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

        private static bool HasSameHost(string hostname, Uri uri)
        {
            if (!EnvironmentHelper.TrustProxy || string.IsNullOrWhiteSpace(hostname) || uri == null)
            {
                return false;
            }

            return string.Equals(
                SSRFDetector.NormalizeHostname(hostname),
                SSRFDetector.NormalizeHostname(uri.Host),
                StringComparison.Ordinal);
        }
    }
}
