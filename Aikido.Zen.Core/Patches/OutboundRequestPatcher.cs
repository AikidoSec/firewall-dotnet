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

        internal static OutboundInspectionResult Inspect(Uri targetUri, string operation, string module, Context context)
        {
            var result = new OutboundInspectionResult();
            var withoutContext = context == null;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                if (context == null)
                {
                    return result;
                }

                if (Agent.Instance.Context.BlockList.IsIPBypassed(context.RemoteAddress))
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
            return MatchesConfiguredAikidoEndpoint(targetUri, EnvironmentHelper.AikidoUrl) ||
                   MatchesConfiguredAikidoEndpoint(targetUri, EnvironmentHelper.AikidoRealtimeUrl);
        }

        private static bool MatchesConfiguredAikidoEndpoint(Uri targetUri, string configuredUrl)
        {
            if (targetUri == null || string.IsNullOrWhiteSpace(configuredUrl))
            {
                return false;
            }

            if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out var configuredUri))
            {
                return false;
            }

            var targetPort = UriHelper.GetPort(targetUri);
            var configuredPort = UriHelper.GetPort(configuredUri);

            return string.Equals(targetUri.Host, configuredUri.Host, StringComparison.OrdinalIgnoreCase) &&
                   targetPort == configuredPort;
        }
    }
}
