using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core.Sinks
{
    internal static class Inspector
    {
        private static readonly AsyncLocal<bool> IsInspecting = new AsyncLocal<bool>();

        internal static bool Inspect(
            MethodBase originalMethod,
            string operationKind,
            Func<Context, InspectionResult> inspect)
        {
            if (IsInspecting.Value)
            {
                return true;
            }

            IsInspecting.Value = true;

            try
            {
                var context = Patcher.GetContext();

                try
                {
                    if (Context.IsBypassed(context) ||
                        Agent.Instance.Context.IsProtectionDisabledForEndpoint(context))
                    {
                        return true;
                    }
                }
                catch
                {
                    LogHelper.ErrorLog(Agent.Logger, $"Error during {operationKind} guard evaluation.");
                    return true;
                }

                var operation = ReflectionHelper.GetMethodOperation(originalMethod);
                var module = ReflectionHelper.GetMethodModule(originalMethod);
                InspectionResult result = InspectionResult.Allow();

                var stopwatch = Stopwatch.StartNew();
                try
                {
                    result = inspect(context) ?? InspectionResult.Allow(skipStats: true);
                }
                catch
                {
                    LogHelper.ErrorLog(Agent.Logger, $"Error during {operationKind} inspection.");
                }
                stopwatch.Stop();

                var isBlocked = result?.AttackKind.HasValue == true && !EnvironmentHelper.DryMode;

                // Blocked outbound connections don't get reported as attacks
                var isAttack = result?.AttackKind.HasValue == true &&
                                        result.AttackKind.Value != AttackKind.OutboundConnectionBlocked;

                try
                {
                    if (isAttack)
                    {
                        Agent.Instance.SendAttackEvent(
                            kind: result.AttackKind.Value,
                            source: result.Source,
                            payload: result.Payload,
                            operation: operation,
                            context: context,
                            module: module,
                            metadata: result.Metadata,
                            blocked: isBlocked,
                            paths: result.Paths);

                        context.AttackDetected = true;
                    }

                    if (!result.SkipStats)
                    {
                        Agent.Instance?.Context?.OnInspectedCall(
                            operation,
                            operationKind,
                            stopwatch.Elapsed.TotalMilliseconds,
                            isAttack,
                            isBlocked,
                            context == null);
                    }
                }
                catch
                {
                    LogHelper.ErrorLog(Agent.Logger, "Error recording stats.");
                }

                if (isBlocked)
                {
                    var blockedOperation = GetBlockedOperation(operation, result);
                    if (result.AttackKind.Value == AttackKind.OutboundConnectionBlocked &&
                        result.Metadata != null &&
                        result.Metadata.TryGetValue("hostname", out var hostname) &&
                        !string.IsNullOrEmpty(hostname))
                    {
                        blockedOperation = $"{operation} to {hostname}";
                    }

                    throw AikidoException.Blocked(
                        result.AttackKind.Value,
                        blockedOperation);
                }

                return true;
            }
            finally
            {
                IsInspecting.Value = false;
            }
        }

        internal static string GetBlockedOperation(string operation, InspectionResult result)
        {
            if (result?.AttackKind == AttackKind.Ssrf && result.Source.HasValue)
            {
                var path = result.Paths.Length > 0 ? result.Paths[0] : string.Empty;
                return $"{operation} originating from {result.Source.Value.ToJsonName()}{path}";
            }

            if (result?.AttackKind == AttackKind.StoredSsrf)
            {
                return $"{operation} originating from unknown source";
            }

            return operation;
        }
    }
}
