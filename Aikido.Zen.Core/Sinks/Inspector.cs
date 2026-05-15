using System;
using System.Diagnostics;
using System.Reflection;
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
            var stopwatch = Stopwatch.StartNew();
            var result = InspectionResult.Continue();

            try
            {
                result = inspect(context) ?? InspectionResult.Continue();

                if (result.AttackDetected && context != null)
                {
                    Agent.Instance.SendAttackEvent(
                        kind: result.AttackKind,
                        source: result.Source,
                        payload: result.Payload,
                        operation: operation,
                        context: context,
                        module: module,
                        metadata: result.Metadata,
                        blocked: result.Blocked,
                        paths: result.Paths);

                    context.AttackDetected = true;
                }
            }
            catch
            {
                LogHelper.ErrorLog(Agent.Logger, $"Error during {operationKind} inspection.");
                result = InspectionResult.Continue();
            }
            finally
            {
                stopwatch.Stop();
                if (result.RecordStats)
                {
                    try
                    {
                        Agent.Instance?.Context?.OnInspectedCall(
                            operation,
                            operationKind,
                            stopwatch.Elapsed.TotalMilliseconds,
                            result.AttackDetected,
                            result.Blocked,
                            context == null);
                    }
                    catch
                    {
                        LogHelper.ErrorLog(Agent.Logger, "Error recording OnInspectedCall stats.");
                    }
                }
            }

            var exceptionToThrow = result.ExceptionFactory?.Invoke(operation);
            if (exceptionToThrow != null)
            {
                throw exceptionToThrow;
            }

            return result.ContinueOriginal;
        }
    }
}
