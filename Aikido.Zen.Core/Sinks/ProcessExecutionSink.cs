using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
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

        private static InspectionResult OnProcessStart(Process process, Context context)
        {
            var result = InspectionResult.Allow();

            try
            {
                var processStartInfo = process?.StartInfo;
                // Only inspect if context and process info are available
                if (processStartInfo != null && context != null)
                {
                    var commands = ShellInjectionDetector.GetCommandsToInspect(processStartInfo);

                    // Inspect the FileName and Arguments for shell injection
                    foreach (var userInput in context.ParsedUserInput)
                    {
                        var command = commands.FirstOrDefault(c => c != null && ShellInjectionDetector.IsShellInjection(c, userInput.Value));
                        if (command == null)
                        {
                            continue;
                        }
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

                        break;
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
