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

            if (targetUri == null || !SSRFDetector.IsSuspiciousTarget(targetUri, context?.Url, out var privateIPAddress))
            {
                return false;
            }

            var hostname = targetUri.Host;
            var port = targetUri.Port;

            if (context?.ParsedUserInput != null)
            {
                foreach (var userInput in context.ParsedUserInput)
                {
                    if (!SSRFDetector.FindTargetInUserInput(userInput.Value, targetUri))
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
