using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Vulnerabilities;

namespace Aikido.Zen.Core.Sinks
{
    /// <summary>
    /// Intercepts and inspects process execution methods to catch and report shell injection attacks.
    /// </summary>
    internal static class ProcessExecutionSink
    {
        private const string kind = "exec_op";

        /// <summary>
        /// Inspects the process start arguments for potential shell injection vulnerabilities.
        /// </summary>
        /// <param name="process">The process being executed.</param>
        /// <param name="originalMethod">The original process method being inspected.</param>
        /// <param name="context">The context of the process execution.</param>
        /// <returns>True if the original method should continue execution; otherwise, false.</returns>
        internal static bool OnProcessStart(Process process, MethodBase originalMethod, Context context)
        {

            // Exclude certain assemblies to avoid stack overflow issues
            if (ReflectionHelper.ShouldSkipAssembly())
            {
                return true;
            }

            if (Context.IsBypassed(context))
            {
                return true;
            }

            var methodInfo = originalMethod as MethodInfo;
            var operation = $"{methodInfo?.DeclaringType?.Name}.{methodInfo?.Name}";
            var module = methodInfo?.DeclaringType?.Assembly.GetName().Name;
            var stopwatch = Stopwatch.StartNew();
            bool withoutContext = context == null;
            bool attackDetected = false;
            bool blocked = false;
            string command; // Store command for logging/stats if needed

            try
            {
                var processStartInfo = process?.StartInfo;
                // Only inspect if context and process info are available
                if (processStartInfo != null && context != null &&
                    !Agent.Instance.Context.IsProtectionDisabledForEndpoint(context))
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
                                source: UserInputHelper.GetAttackSourceFromUserInputKey(userInput.Key),
                                payload: userInput.Value,
                                operation: operation,
                                context: context,
                                module: module,
                                metadata: metadata,
                                blocked: blocked,
                                paths: new[] { UserInputHelper.GetAttackPathFromUserInputKey(userInput.Key) }
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
