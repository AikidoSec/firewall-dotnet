using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Vulnerabilities;

namespace Aikido.Zen.Core.Patches
{
    public static class ProcessExecutionSink
    {
        private const string OperationKind = "exec_op";

        [PatchTarget(PatchKind.Prefix, new string[] { "System.Diagnostics.Process", "System" }, "System.Diagnostics.Process", "Start")]
        private static bool OnProcessStart(object[] __args, MethodBase __originalMethod, object __instance)
        {
            return OnProcessStart(__args, __originalMethod, __instance, Patcher.GetContext());
        }

        public static bool OnProcessStart(object[] __args, MethodBase __originalMethod, object __instance, Context context)
        {
            if (ReflectionHelper.ShouldSkipAssembly())
            {
                return true;
            }

            if (Context.IsBypassed(context))
            {
                return true;
            }

            var stopwatch = Stopwatch.StartNew();
            var methodInfo = __originalMethod as MethodInfo;
            var operation = $"{methodInfo?.DeclaringType?.Name}.{methodInfo?.Name}";
            var assemblyName = methodInfo?.DeclaringType?.Assembly.GetName().Name;
            var withoutContext = context == null;
            var attackDetected = false;
            var blocked = false;

            try
            {
                var processStartInfo = (__instance as Process)?.StartInfo;
                if (processStartInfo != null && context != null &&
                    !Agent.Instance.Context.IsProtectionDisabledForEndpoint(context))
                {
                    var command = processStartInfo.FileName + " " + processStartInfo.Arguments;

                    foreach (var userInput in context.ParsedUserInput)
                    {
                        if (ShellInjectionDetector.IsShellInjection(command, userInput.Value))
                        {
                            attackDetected = true;
                            blocked = !EnvironmentHelper.DryMode;

                            var metadata = new Dictionary<string, object> {
                                { "command", command }
                            };
                            Agent.Instance.SendAttackEvent(
                                kind: AttackKind.ShellInjection,
                                source: UserInputHelper.GetAttackSourceFromUserInputKey(userInput.Key),
                                payload: userInput.Value,
                                operation: operation,
                                context: context,
                                module: assemblyName,
                                metadata: metadata,
                                blocked: blocked,
                                paths: new[] { UserInputHelper.GetAttackPathFromUserInputKey(userInput.Key) }
                            );
                            context.AttackDetected = true;
                            break;
                        }
                    }
                }
            }
            catch
            {
                LogHelper.ErrorLog(Agent.Logger, "Error during Shell injection detection.");
                attackDetected = false;
                blocked = false;
            }

            stopwatch.Stop();
            try
            {
                Agent.Instance?.Context?.OnInspectedCall(operation, OperationKind, stopwatch.Elapsed.TotalMilliseconds, attackDetected, blocked, withoutContext);
            }
            catch
            {
                LogHelper.ErrorLog(Agent.Logger, "Error recording Process.Start OnInspectedCall stats.");
            }

            if (blocked)
            {
                throw AikidoException.ShellInjectionDetected();
            }

            return true;
        }
    }
}
