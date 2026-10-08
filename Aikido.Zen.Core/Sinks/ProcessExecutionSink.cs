using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
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
        private const string OperationKind = "exec_op";

        [SinkPrefix(typeof(Process), "Start")]
        internal static bool OnProcessStartInstance(Process __instance, MethodBase __originalMethod)
        {
            return Inspector.Inspect(
                __originalMethod,
                OperationKind,
                Patcher.GetContext(),
                context => OnProcessStart(__instance, context));
        }

        /// <summary>
        /// Inspects the process start arguments for potential shell injection vulnerabilities.
        /// </summary>
        /// <param name="process">The process being executed.</param>
        /// <param name="context">The context of the process execution.</param>
        /// <returns>The inspection result. Contains a blocking exception if a blocked attack is detected.</returns>
        private static InspectionResult OnProcessStart(Process process, Context context)
        {
            var result = InspectionResult.Allow();
            string command; // Store command for logging/stats if needed

            try
            {
                var processStartInfo = process?.StartInfo;
                // Only inspect if context and process info are available
                if (processStartInfo != null && context != null)
                {
                    // Build the complete command including both Arguments and ArgumentList
                    var commandBuilder = new StringBuilder();
                    commandBuilder.Append(processStartInfo.FileName);
                    
                    // Add legacy Arguments property if present
                    if (!string.IsNullOrEmpty(processStartInfo.Arguments))
                    {
                        commandBuilder.Append(" ");
                        commandBuilder.Append(processStartInfo.Arguments);
                    }
                    
                    // Add modern ArgumentList property if present
                    // ArgumentList is available in .NET Core 2.1+ and .NET Standard 2.1+
                    if (processStartInfo.ArgumentList != null && processStartInfo.ArgumentList.Count > 0)
                    {
                        foreach (var arg in processStartInfo.ArgumentList)
                        {
                            commandBuilder.Append(" ");
                            commandBuilder.Append(arg);
                        }
                    }
                    
                    command = commandBuilder.ToString();

                    // Inspect the FileName and Arguments for shell injection
                    foreach (var userInput in context.ParsedUserInput)
                    {
                        if (ShellInjectionDetector.IsShellInjection(command, userInput.Value))
                        {
                            // Log or throw an exception to report the issue
                            var metadata = new Dictionary<string, string> {
                                { "command", command }
                            };

                            result = InspectionResult.Block(
                                AttackKind.ShellInjection,
                                UserInputHelper.GetAttackSourceFromUserInputKey(userInput.Key),
                                userInput.Value,
                                metadata,
                                new[] { UserInputHelper.GetAttackPathFromUserInputKey(userInput.Key) }
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
                return InspectionResult.Allow();
            }

            return result;
        }

    }
}
