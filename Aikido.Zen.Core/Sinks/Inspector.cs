using System;
using System.Diagnostics;
using System.Reflection;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core.Sinks
{
    internal static class Inspector
    {
        internal static bool Inspect(
            MethodBase originalMethod,
            string operationKind,
            Func<Context, InspectionResult> inspect)
        {
            var context = Patcher.GetContext();

            try
            {
                if (ReflectionHelper.ShouldSkipAssembly() ||
                    Context.IsBypassed(context) ||
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
            var isAttack = context != null &&
                                    result?.AttackKind.HasValue == true &&
                                    result.AttackKind.Value != AttackKind.OutboundConnectionBlocked;

            try
            {
                if (isAttack)
                {
                    Agent.Instance.SendAttackEvent(
                        kind: result.AttackKind.Value,
                        source: result.Source.Value,
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
                throw AikidoException.Blocked(
                    result.AttackKind.Value,
                    operation,
                    result.Metadata == null ? null : string.Join(", ", result.Metadata.Values));
            }

            return true;
        }
    }
}
