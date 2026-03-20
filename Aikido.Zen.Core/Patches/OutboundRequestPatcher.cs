using System;
using System.Diagnostics;
using System.Threading;
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
        private static readonly AsyncLocal<OutboundRequestInfo> CurrentRequest = new AsyncLocal<OutboundRequestInfo>();

        internal static OutboundRequestInfo CurrentRequestScope => CurrentRequest.Value;

        internal static void Inspect(Uri targetUri, string operation, string module, Context context)
        {
            var withoutContext = context == null;
            var stopwatch = Stopwatch.StartNew();
            var attackDetected = false;
            var blocked = false;

            try
            {
                if (Agent.Instance.Context.BlockList.IsIPBypassed(context?.RemoteAddress))
                {
                    return;
                }

                var hostname = targetUri.Host;
                var port = targetUri.Port;

                Agent.Instance.CaptureOutboundRequest(hostname, port);

                if (IsAikidoInternalTarget(targetUri))
                {
                    return;
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
                    return;
                }

                // SSRF skip non-suspicious requests
                if (!SSRFHelper.IsSuspiciousRequest(targetUri, context, module, operation, out var privateIPAddress))
                {
                    return;
                }

                if (privateIPAddress != null)
                {
                    // Request suspicious and we already have the ip
                    attackDetected = SSRFHelper.DetectSSRF(targetUri, privateIPAddress, context, module, operation, out AttackKind? attackKind, out string source);
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
                }
                else
                {
                    // Request suspicious but we only have hostname and no resolution yet
                    // Dns will come in later and resolve the ip
                    EnterRequestScope(targetUri, operation, module);
                }
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

        internal static void ExitRequestScope()
        {
            CurrentRequest.Value = null;
        }

        internal static void RecordDetectedAttack(AttackKind attackKind, string source)
        {
            var currentRequest = CurrentRequest.Value;
            if (currentRequest == null)
            {
                return;
            }

            currentRequest.DetectedAttackKind = attackKind;
            currentRequest.DetectedAttackSource = source;
        }

        internal static bool TryGetDetectedAttackException(out AikidoException exception)
        {
            exception = null;
            var requestInfo = CurrentRequest.Value;

            if (requestInfo?.DetectedAttackKind == null)
            {
                return false;
            }

            if (requestInfo.DetectedAttackKind == AttackKind.StoredSsrf)
            {
                exception = AikidoException.StoredSSRFDetected(requestInfo.Operation);
                return true;
            }

            if (requestInfo.DetectedAttackKind == AttackKind.Ssrf)
            {
                exception = AikidoException.SSRFDetected(requestInfo.Operation, requestInfo.DetectedAttackSource);
                return true;
            }

            return false;
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

        private static void EnterRequestScope(Uri targetUri, string operation, string module)
        {
            CurrentRequest.Value = new OutboundRequestInfo(targetUri, operation, module);
        }

        internal sealed class OutboundRequestInfo
        {
            internal OutboundRequestInfo(Uri targetUri, string operation, string module)
            {
                TargetUri = targetUri;
                Operation = operation;
                Module = module;
            }

            internal Uri TargetUri { get; }
            internal string Operation { get; }
            internal string Module { get; }
            internal AttackKind? DetectedAttackKind { get; set; }
            internal string DetectedAttackSource { get; set; }
        }
    }
}
