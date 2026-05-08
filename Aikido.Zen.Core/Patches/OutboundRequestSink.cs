using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Reflection;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core.Patches
{
    internal sealed class OutboundInspectionResult
    {
        public bool ShouldProceed { get; set; } = true;
        public bool AttackDetected { get; set; }
        public bool Blocked { get; set; }
        public Exception Exception { get; set; }
    }

    internal static class OutboundRequestSink
    {
        private const string OperationKind = "outgoing_http_op";
        private static readonly Uri ParsedAikidoUrl = new Uri(EnvironmentHelper.AikidoUrl);
        private static readonly Uri ParsedAikidoRealtimeUrl = new Uri(EnvironmentHelper.AikidoRealtimeUrl);

        internal static bool OnRequest(object[] __args, MethodBase __originalMethod, object __instance)
        {
            var targetUri = ResolveOutboundUri(__args, __instance);
            if (targetUri == null)
            {
                return true;
            }

            var inspection = Inspect(
                targetUri,
                GetOperation(__originalMethod),
                GetModule(__originalMethod),
                Patcher.GetContext());

            if (inspection.ShouldProceed)
            {
                return true;
            }

            throw inspection.Exception;
        }

        internal static OutboundInspectionResult Inspect(Uri targetUri, string operation, string module, Context context)
        {
            var result = new OutboundInspectionResult();
            var withoutContext = context == null;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var hostname = targetUri.Host;
                var port = UriHelper.GetPort(targetUri);
                Agent.Instance.CaptureOutboundRequest(hostname, port);

                if (Context.IsBypassed(context))
                {
                    return result;
                }

                if (EnvironmentHelper.DryMode || IsAikidoInternalTarget(targetUri))
                {
                    return result;
                }

                if (Agent.Instance.Context.Config.ShouldBlockOutgoingRequest(hostname))
                {
                    result.ShouldProceed = false;
                    result.Blocked = true;
                    result.Exception = AikidoException.OutboundConnectionBlocked(hostname);
                    return result;
                }

                if (Agent.Instance.Context.IsProtectionDisabledForEndpoint(context))
                {
                    return result;
                }

                return result;
            }
            finally
            {
                stopwatch.Stop();
                Agent.Instance?.Context?.OnInspectedCall(
                    operation,
                    OperationKind,
                    stopwatch.Elapsed.TotalMilliseconds,
                    result.AttackDetected,
                    result.Blocked,
                    withoutContext);
            }
        }

        private static bool IsAikidoInternalTarget(Uri targetUri)
        {
            if (targetUri == null)
            {
                return false;
            }

            var targetPort = UriHelper.GetPort(targetUri);
            var targetHost = targetUri.Host;

            if (string.Equals(targetHost, ParsedAikidoUrl.Host, StringComparison.OrdinalIgnoreCase) &&
                targetPort == UriHelper.GetPort(ParsedAikidoUrl))
            {
                return true;
            }

            if (string.Equals(targetHost, ParsedAikidoRealtimeUrl.Host, StringComparison.OrdinalIgnoreCase) &&
                targetPort == UriHelper.GetPort(ParsedAikidoRealtimeUrl))
            {
                return true;
            }

            return false;
        }

        private static Uri ResolveOutboundUri(object[] args, object instance)
        {
            var httpClient = instance as HttpClient;
            if (httpClient != null)
            {
                var request = args != null && args.Length > 0 ? args[0] as HttpRequestMessage : null;
                return ResolveUri(request, httpClient);
            }

            var webRequest = instance as WebRequest;
            return webRequest?.RequestUri;
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
