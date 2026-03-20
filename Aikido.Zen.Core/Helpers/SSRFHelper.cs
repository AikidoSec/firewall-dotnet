using System;
using System.Collections.Generic;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Vulnerabilities;

namespace Aikido.Zen.Core.Helpers
{
    public static class SSRFHelper
    {
        public static bool IsSuspiciousRequest(Uri targetUri, Context context, string moduleName, string operation, out string privateIPAddress)
        {
            privateIPAddress = null;

            if (targetUri == null)
            {
                return false;
            }

            Uri.TryCreate(context?.Url, UriKind.Absolute, out var serverUri);
            if (SSRFDetector.IsRequestToItself(serverUri, targetUri))
            {
                return false;
            }

            SSRFDetector.TryGetPrivateOrLocalIPAddress(targetUri.Host, out privateIPAddress);
            return true;
        }

        internal static bool DetectSSRF(Uri targetUri, string privateIPAddress, Context context, string moduleName, string operation, out AttackKind? attackKind, out string source)
        {
            attackKind = null;
            source = null;

            if (privateIPAddress == null)
            {
                // We need the ip address at this point
                return false;
            }

            var hostname = targetUri.Host;
            var port = targetUri.Port;

            if (!SSRFDetector.IsRequestToServiceHostname(hostname) && context?.ParsedUserInput != null)
            {
                foreach (var userInput in context.ParsedUserInput)
                {
                    Uri.TryCreate(userInput.Value, UriKind.Absolute, out var userUri);
                    if (!SSRFDetector.HasSameHostAndPort(targetUri, userUri))
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
                    attackKind = AttackKind.Ssrf;
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

                attackKind = AttackKind.StoredSsrf;
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
