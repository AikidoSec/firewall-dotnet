using System;
using System.Diagnostics;
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

    internal static class OutboundRequestPatcher
    {
        private const string OperationKind = "outgoing_http_op";
        private static readonly Uri ParsedAikidoUrl = new Uri(EnvironmentHelper.AikidoUrl);
        private static readonly Uri ParsedAikidoRealtimeUrl = new Uri(EnvironmentHelper.AikidoRealtimeUrl);

        internal static OutboundInspectionResult Inspect(Uri targetUri, string operation, string module, Context context)
        {
            var result = new OutboundInspectionResult();
            var withoutContext = context == null;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                if (Agent.Instance.Context.BlockList.IsIPBypassed(context?.RemoteAddress))
                {
                    return result;
                }

                var hostname = targetUri.Host;
                var port = UriHelper.GetPort(targetUri);
                Agent.Instance.CaptureOutboundRequest(hostname, port);

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
    }
}
