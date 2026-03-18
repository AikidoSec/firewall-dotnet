using System;
using System.Collections.Generic;
using System.Threading;
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

        internal static RequestScopeState EnterRequestScope(Uri targetUri, string operation, string module)
        {
            var previous = CurrentRequest.Value;
            CurrentRequest.Value = new OutboundRequestInfo(targetUri, operation, module);
            return new RequestScopeState(previous);
        }

        internal static void ExitRequestScope(RequestScopeState state)
        {
            if (state == null || !state.Entered)
            {
                return;
            }

            CurrentRequest.Value = state.PreviousRequest;
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
        }

        internal sealed class RequestScopeState
        {
            internal RequestScopeState(OutboundRequestInfo previousRequest)
            {
                PreviousRequest = previousRequest;
                Entered = true;
            }

            internal OutboundRequestInfo PreviousRequest { get; }
            internal bool Entered { get; }
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
