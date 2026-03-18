using System;
using System.Diagnostics;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core.Patches
{
    internal static class OutboundRequestPatcher
    {
        private const string OperationKind = "outgoing_http_op";
        private static readonly Uri ParsedAikidoUrl = new Uri(EnvironmentHelper.AikidoUrl);
        private static readonly Uri ParsedAikidoRealtimeUrl = new Uri(EnvironmentHelper.AikidoRealtimeUrl);

        internal static bool Inspect(Uri targetUri, string operation, string module, Context context)
        {
            var withoutContext = context == null;
            var stopwatch = Stopwatch.StartNew();
            var attackDetected = false;
            var blocked = false;

            try
            {
                if (Agent.Instance.Context.BlockList.IsIPBypassed(context?.RemoteAddress))
                {
                    return true;
                }

                var hostname = targetUri.Host;
                var port = targetUri.Port;

                Agent.Instance.CaptureOutboundRequest(hostname, port);

                if (IsAikidoInternalTarget(targetUri))
                {
                    // Skip inspecting Aikido requests
                    return true;
                }

                if (Agent.Instance.Context.Config.ShouldBlockOutgoingRequest(hostname))
                {
                    if (!EnvironmentHelper.DryMode)
                    {
                        blocked = true;
                        throw AikidoException.OutboundConnectionBlocked(hostname);
                    }
                }

                if (Agent.Instance.Context.IsProtectionDisabledForEndpoint(context))
                {
                    return true;
                }

                attackDetected = SSRFHelper.DetectSSRF(targetUri, context, module, operation, out var attackKind, out var source);
                blocked = attackDetected && !EnvironmentHelper.DryMode;

                if (blocked)
                {
                    if (attackKind == AttackKind.StoredSsrf)
                    {
                        throw AikidoException.StoredSSRFDetected(operation);
                    }

                    if (attackKind == AttackKind.Ssrf)
                    {
                        throw AikidoException.SSRFDetected(operation, source);
                    }

                    throw new InvalidOperationException("SSRF attack detected without a concrete attack kind.");
                }

                if (Agent.Instance.Context.IsProtectionDisabledForEndpoint(context))
                {
                    return true;
                }

                return true;
            }
            finally
            {
                stopwatch.Stop();
                Agent.Instance?.Context?.OnInspectedCall(
                    operation,
                    OperationKind,
                    stopwatch.Elapsed.TotalMilliseconds,
                    attackDetected,
                    blocked,
                    withoutContext);
            }
        }

        private static bool IsAikidoInternalTarget(Uri targetUri)
        {
            if (targetUri == null)
            {
                return false;
            }

            var targetHost = targetUri.Host;
            var targetPort = targetUri.Port;

            if (string.Equals(targetHost, ParsedAikidoUrl.Host, StringComparison.OrdinalIgnoreCase) &&
                targetPort == ParsedAikidoUrl.Port)
            {
                return true;
            }

            if (string.Equals(targetHost, ParsedAikidoRealtimeUrl.Host, StringComparison.OrdinalIgnoreCase) &&
                targetPort == ParsedAikidoRealtimeUrl.Port)
            {
                return true;
            }

            return false;
        }
    }
}
