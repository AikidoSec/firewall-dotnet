using System;
using System.Collections.Generic;
using System.Threading;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Vulnerabilities;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// A helper class for outbound request tracking
    /// </summary>
    public class OutboundRequestHelper
    {
        private static readonly AsyncLocal<OutboundRequestInfo> CurrentRequest = new AsyncLocal<OutboundRequestInfo>();

        internal static OutboundRequestInfo CurrentRequestScope => CurrentRequest.Value;

        internal static void EnterRequestScope(Uri targetUri, string operation, string module)
        {
            var current = new OutboundRequestInfo(targetUri, operation, module);
            CurrentRequest.Value = current;
        }

        internal static void ExitRequestScope()
        {
            CurrentRequest.Value = null;
        }

        public static bool InspectRequest(Uri targetUri, Context context, string moduleName, string operation, out AttackKind? attackKind, out string source)
        {
            attackKind = null;
            source = null;

            if (targetUri == null)
            {
                return false;
            }

            Uri.TryCreate(context?.Url, UriKind.Absolute, out var serverUri);
            if (SSRFDetector.HasSameHostAndPort(targetUri, serverUri))
            {
                return false;
            }

            if (SSRFDetector.TryGetPrivateOrLocalIPAddress(targetUri.Host, out var privateIPAddress))
            {
                return DetectSSRF(targetUri, privateIPAddress, context, moduleName, operation, out attackKind, out source);
            }

            return false;
        }

        internal static bool DetectSSRF(Uri targetUri, string privateIPAddress, Context context, string moduleName, string operation, out AttackKind? attackKind, out string source)
        {
            attackKind = null;
            source = null;

            if (privateIPAddress == null)
            {
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
                    RecordDetectedAttack(AttackKind.Ssrf, $"{attackSource.ToJsonName()}{attackPath}");
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

                RecordDetectedAttack(AttackKind.StoredSsrf, "unknown source");
                attackKind = AttackKind.StoredSsrf;
                source = "unknown source";
                return true;
            }

            return false;
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
    }
}
