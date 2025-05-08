using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Vulnerabilities;
using HarmonyLib;

namespace Aikido.Zen.Core.Patches
{
    /// <summary>
    /// Intercepts and inspects process execution methods to catch and report shell injection attacks.
    /// </summary>
    public static class ProcessExecutionPatcher
    {
        private const string kind = "exec_op";
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
            var stopwatch = Stopwatch.StartNew();
            var methodInfo = __originalMethod as MethodInfo;
            var operation = $"{methodInfo?.DeclaringType?.Name}.{methodInfo?.Name}";
            var assemblyName = methodInfo?.DeclaringType?.Assembly.GetName().Name;
            bool withoutContext = context == null;
            bool attackDetected = false;
            bool blocked = false;
            string command; // Store command for logging/stats if needed

            try
            {
                var processStartInfo = (__instance as Process)?.StartInfo;
                // Only inspect if context and process info are available
                if (processStartInfo != null && context != null)
                {
                    command = processStartInfo.FileName + " " + processStartInfo.Arguments;

                    // Inspect the FileName and Arguments for shell injection
                    foreach (var userInput in context.ParsedUserInput)
                    {
                        if (ShellInjectionDetector.IsShellInjection(command, userInput.Value))
                        {
                            attackDetected = true;
                            blocked = !EnvironmentHelper.DryMode;

                            // Log or throw an exception to report the issue
                            var metadata = new Dictionary<string, object> {
                                { "command", command }
                            };
                            Agent.Instance.SendAttackEvent(
                                kind: AttackKind.ShellInjection,
                                source: HttpHelper.GetSourceFromUserInputPath(userInput.Key),
                                payload: userInput.Value,
                                operation: operation,
                                context: context,
                                module: assemblyName,
                                metadata: metadata,
                                blocked: blocked
                            );
                            context.AttackDetected = true; // Mark context
                            // Break after first detection for this process start
                            break;
                        }
                    }
                }
            }
            catch
            {
                // Error during detection logic
                LogHelper.ErrorLog(Agent.Logger, "Error during Shell injection detection.");
                attackDetected = false; // Reset flags as detection failed
                blocked = false;
            }

            stopwatch.Stop();
            // Record the call attempt statistics
            try
            {
                Agent.Instance?.Context?.OnInspectedCall(operation, kind, stopwatch.Elapsed.TotalMilliseconds, attackDetected, blocked, withoutContext);
            }
            catch
            {
                LogHelper.ErrorLog(Agent.Logger, "Error recording Process.Start OnInspectedCall stats.");
            }

            // Handle blocking
            if (blocked)
            {
                throw AikidoException.ShellInjectionDetected();
            }

            return true; // Allow original method execution
        }
    }
}
