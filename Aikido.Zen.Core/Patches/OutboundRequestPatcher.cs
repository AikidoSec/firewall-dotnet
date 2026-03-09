using System;
using System.Diagnostics;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Vulnerabilities;

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
                if (context != null && Agent.Instance.Context.BlockList.IsIPBypassed(context.RemoteAddress))
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

                if (InspectSsrf(targetUri, operation, module, context, result))
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

        private static bool InspectSsrf(Uri targetUri, string operation, string module, Context context, OutboundInspectionResult result)
        {
            var detection = SsrfDetector.Detect(targetUri, context);
            if (detection == null)
            {
                return false;
            }

            var blocked = !EnvironmentHelper.DryMode;
            // Stored SSRF can be reported without a matching request-scoped source, payload, or context.
            Agent.Instance.SendAttackEvent(
                detection.Kind,
                detection.Source,
                detection.Payload,
                operation,
                detection.Kind == AttackKind.StoredSsrf ? null : context,
                module,
                detection.Metadata,
                blocked);

            if (context != null)
            {
                context.AttackDetected = true;
            }

            result.AttackDetected = true;
            result.Blocked = blocked;
            if (blocked)
            {
                result.ShouldProceed = false;
                result.Exception = detection.Kind == AttackKind.StoredSsrf
                    ? AikidoException.StoredSsrfDetected()
                    : AikidoException.SsrfDetected();
            }
            return true;
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
