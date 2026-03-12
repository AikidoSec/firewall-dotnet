using System;
using System.Collections.Generic;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Vulnerabilities;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// A helper class for SSRF detection
    /// </summary>
    public class SSRFHelper
    {
        public static bool DetectSSRF(Uri targetUri, Context context, string moduleName, string operation, out string attackKind, out string source)
        {
            attackKind = null;
            source = null;

            Uri.TryCreate(context?.Url, UriKind.Absolute, out var serverUri);

            if (!SSRFDetector.IsSuspiciousTarget(targetUri, serverUri, out var privateIPAddress))
            {
                return false;
            }

            var hostname = targetUri.Host;
            var port = targetUri.Port;

            if (context?.ParsedUserInput != null)
            {
                foreach (var userInput in context.ParsedUserInput)
                {
                    Uri.TryCreate(userInput.Value, UriKind.Absolute, out var userUri);
                    if (!MatchesTargetOrRedirectedSource(targetUri, userUri, context))
                    {
                        continue;
                    }

                    var attackSource = UserInputHelper.GetAttackSourceFromUserInputKey(userInput.Key);
                    var attackPath = UserInputHelper.GetAttackPathFromUserInputKey(userInput.Key);

                    Agent.Instance.SendAttackEvent(
                        kind: AttackKind.Ssrf,
                        source: attackSource,
                        payload: userInput.Value,
                        operation: operation,
                        context: context,
                        module: moduleName,
                        metadata: CreateMetadata(hostname, port, privateIPAddress),
                        blocked: !EnvironmentHelper.DryMode,
                        paths: new[] { attackPath }
                    );

                    context.AttackDetected = true;
                    attackKind = AttackKind.Ssrf.ToHumanName();
                    source = $"{attackSource.ToJsonName()}{attackPath}";
                    return true;
                }
            }

            if (SSRFDetector.IsStoredSSRF(hostname, privateIPAddress))
            {
                Agent.Instance.SendAttackEvent(
                    kind: AttackKind.StoredSsrf,
                    source: null,
                    payload: null,
                    operation: operation,
                    context: context,
                    module: moduleName,
                    metadata: CreateMetadata(hostname, null, privateIPAddress),
                    blocked: !EnvironmentHelper.DryMode,
                    paths: Array.Empty<string>()
                );

                attackKind = AttackKind.StoredSsrf.ToHumanName();
                source = "unknown source";
                return true;
            }

            return false;
        }

        private static bool MatchesTargetOrRedirectedSource(Uri targetUri, Uri userUri, Context context)
        {
            if (SSRFDetector.HasSameHostAndPort(targetUri, userUri))
            {
                return true;
            }

            if (context?.OutgoingRequestRedirects == null)
            {
                return false;
            }

            foreach (var redirect in context.OutgoingRequestRedirects)
            {
                if (SSRFDetector.HasSameHostAndPort(userUri, redirect.Source) &&
                    SSRFDetector.HasSameHostAndPort(targetUri, redirect.Destination))
                {
                    return true;
                }
            }

            return false;
        }

        private static IDictionary<string, object> CreateMetadata(string hostname, int? port, string privateIPAddress)
        {
            var metadata = new Dictionary<string, object>
            {
                ["hostname"] = hostname
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
    }
}
