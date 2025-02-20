using System;
using System.Diagnostics;
using System.Reflection;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using System.Collections.Generic;
using Aikido.Zen.Core.Vulnerabilities;

namespace Aikido.Zen.Core.Patches
{
    /// <summary>
    /// Intercepts and inspects process execution methods to catch and report shell injection attacks.
    /// </summary>
    public static class ProcessExecutionPatcher
    {
        /// <summary>
        /// Inspects the process start arguments for potential shell injection vulnerabilities.
        /// </summary>
        /// <param name="__args">The arguments passed to the original method.</param>
        /// <param name="__originalMethod">The original method being patched.</param>
        /// <param name="__instance">The instance of the process being executed.</param>
        /// <param name="context">The context of the process execution.</param>
        /// <returns>True if the original method should continue execution; otherwise, false.</returns>
        public static bool OnProcessStart(object[] __args, MethodBase __originalMethod, object __instance, Context context)
        {
            var processStartInfo = (__instance as Process)?.StartInfo;
            if (processStartInfo == null || context == null) return true;

            // Inspect the FileName and Arguments for shell injection
            foreach (var userInput in context.ParsedUserInput)
            {
                var command = processStartInfo.FileName + " " + processStartInfo.Arguments;
                var timer = StatHelper.StartTimer();
                var isShellInjection = ShellInjectionDetector.IsShellInjection(command, userInput.Value);
                var duration = timer.StopTimer();
                Agent.Instance.Context.AddSinkStat(
                    sink: __originalMethod.DeclaringType?.FullName,
                    blocked: !EnvironmentHelper.DryMode,
                    attackDetected: isShellInjection,
                    durationInMs: duration,
                    withoutContext: false
                );
                if (isShellInjection)
                {
                    // Log or throw an exception to report the issue
                    var metadata = new Dictionary<string, object> {
                        { "command", command }
                    };
                    Agent.Instance.SendAttackEvent(
                        kind: AttackKind.ShellInjection,
                        source: HttpHelper.GetSourceFromUserInputPath(userInput.Key),
                        payload: userInput.Value,
                        operation: __originalMethod.Name,
                        context: context,
                        module: __originalMethod.DeclaringType?.FullName,
                        metadata: metadata,
                        blocked: !EnvironmentHelper.DryMode
                    );
                    context.AttackDetected = true;
                    if (!EnvironmentHelper.DryMode)
                    {
                        throw AikidoException.ShellInjectionDetected();
                    }

                }
            }

            return true;
        }
    }
}
