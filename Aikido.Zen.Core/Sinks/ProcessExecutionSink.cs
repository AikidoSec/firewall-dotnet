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
        internal const string OperationKind = "exec_op";

        [SinkPrefix(typeof(Process), "Start")]
        internal static bool OnProcessStartInstance(Process __instance, MethodBase __originalMethod)
        {
            return SinkAnalyzer.Analyze(
                __originalMethod,
                OperationKind,
                context => OnProcessStart(__instance, context));
        }

        /// <summary>
        /// Inspects the process start arguments for potential shell injection vulnerabilities.
        /// </summary>
        /// <param name="process">The process being executed.</param>
        /// <param name="context">The context of the process execution.</param>
        /// <returns>The inspection result. Contains a blocking exception if a blocked attack is detected.</returns>
        internal static InspectionResult OnProcessStart(Process process, Context context)
        {
            var result = InspectionResult.Continue();
            string command; // Store command for logging/stats if needed

            try
            {
                var processStartInfo = process?.StartInfo;
                // Only inspect if context and process info are available
                if (processStartInfo != null && context != null)
                {
                    command = processStartInfo.FileName + " " + processStartInfo.Arguments;

                    // Inspect the FileName and Arguments for shell injection
                    foreach (var userInput in context.ParsedUserInput)
                    {
                        if (ShellInjectionDetector.IsShellInjection(command, userInput.Value))
                        {
                            // Log or throw an exception to report the issue
                            var metadata = new Dictionary<string, object> {
                                { "command", command }
                            };

                            result = InspectionResult.Attack(
                                AttackKind.ShellInjection,
                                UserInputHelper.GetAttackSourceFromUserInputKey(userInput.Key),
                                userInput.Value,
                                metadata,
                                new[] { UserInputHelper.GetAttackPathFromUserInputKey(userInput.Key) },
                                !EnvironmentHelper.DryMode,
                                AikidoException.ShellInjectionDetected()
                            );

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
                return InspectionResult.Continue();
            }

            return result;
        }

    }
}
