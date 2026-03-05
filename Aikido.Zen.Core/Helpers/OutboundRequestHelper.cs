using System;
using System.Diagnostics;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core.Helpers
{
    internal sealed class OutboundInspectionResult
    {
        public bool ShouldProceed { get; set; } = true;
        public bool AttackDetected { get; set; }
        public bool Blocked { get; set; }
        public Exception Exception { get; set; }
    }

    internal static class OutboundRequestHelper
    {
        private const string OperationKind = "outgoing_http_op";

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

                if (IsBypassedRequest(context))
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

        private static bool IsBypassedRequest(Context context)
        {
            return context != null &&
                !string.IsNullOrWhiteSpace(context.RemoteAddress) &&
                Agent.Instance.Context.BlockList.IsIPBypassed(context.RemoteAddress);
        }
    }
}
