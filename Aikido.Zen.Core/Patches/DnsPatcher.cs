using System;
using System.Net;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core.Patches
{
    internal static class DnsPatcher
    {
        internal static void Inspect(string hostNameOrAddress, IPAddress[] resolvedAddresses, Context context)
        {
            var outboundRequest = OutboundRequestHelper.CurrentRequestScope;
            if (outboundRequest == null)
            {
                return;
            }

            if (resolvedAddresses == null || resolvedAddresses.Length == 0)
            {
                return;
            }

            var privateIPAddress = resolvedAddresses[0]?.ToString();
            if (string.IsNullOrWhiteSpace(privateIPAddress) || !IPHelper.IsPrivateOrLocalIp(privateIPAddress))
            {
                return;
            }

            var attackDetected = OutboundRequestHelper.DetectSSRF(
                outboundRequest.TargetUri,
                privateIPAddress,
                context,
                outboundRequest.Module,
                outboundRequest.Operation,
                out var attackKind,
                out var source);

            if (!attackDetected || EnvironmentHelper.DryMode)
            {
                return;
            }

            // Record for potential later use by our HttpClient postfix
            // AikidoException will be swallowed by HttpClient, but will be rethrown from our finalizer
            if (attackKind == AttackKind.StoredSsrf)
            {
                OutboundRequestHelper.RecordDetectedAttack(AttackKind.StoredSsrf, "unknown source");
                throw AikidoException.StoredSSRFDetected(outboundRequest.Operation);
            }

            if (attackKind == AttackKind.Ssrf)
            {
                OutboundRequestHelper.RecordDetectedAttack(AttackKind.Ssrf, source);
                throw AikidoException.SSRFDetected(outboundRequest.Operation, source);
            }

            throw new InvalidOperationException("SSRF attack detected without a concrete attack kind.");
        }
    }
}
