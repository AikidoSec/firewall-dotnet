using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
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

        // Decode process-argument escapes so paths such as C:\temp stay intact and \" supplies a literal quote.
        // Before a quote, backslash pairs become one backslash; an odd remainder escapes the quote.
        private const string literalBackslashes = @"(?<text>\\+)(?![\\""])";
        private const string backslashPairs = @"(?:(?<text>\\)\\)*";
        private const string escapedQuote = backslashPairs + @"\\(?<text>"")";
        // Inside quotes, doubled quotes are literal ("a""b" becomes a"b); .NET also accepts an unclosed quote.
        private static readonly string quotedPart = $@"
            {backslashPairs}""
            (?> (?<text>[^\\""]+) | {literalBackslashes} | {escapedQuote} | {backslashPairs}""(?<text>"") )*
            (?: {backslashPairs}"" | \z )";
        // Process.Start only treats space and tab as separators; a newline belongs to the argument.
        // Atomic groups avoid repeated work on long input by preventing backtracking over consumed separators and fragments.
        private static readonly Regex processArguments = new Regex($@"
            \G(?>[ \t]*)
            (?> (?<text>[^ \t\\""]+) | {literalBackslashes} | {escapedQuote} | {quotedPart} )+",
            RegexOptions.IgnorePatternWhitespace);

        // ArgumentList is not part of netstandard2.0.
        private static readonly PropertyInfo argumentListProperty = typeof(ProcessStartInfo).GetProperty("ArgumentList");

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
                    var commands = ShellInjectionDetector.GetCommandsToInspect(
                        processStartInfo.FileName, processStartInfo.Arguments, GetArguments(processStartInfo));

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

        // Arguments contains all process arguments in one string, so we split and decode it before inspection.
        // ArgumentList already contains separate argument values and needs no decoding.
        internal static List<string> GetArguments(ProcessStartInfo startInfo)
        {
            var arguments = string.IsNullOrEmpty(startInfo.Arguments)
                ? GetArgumentList(startInfo)
                : DecodeProcessArguments(startInfo.Arguments);
            return arguments.ToList();
        }

        internal static IEnumerable<string> DecodeProcessArguments(string arguments)
        {
            // Quoted and unquoted fragments can form one argument: "echo hello";whoami becomes echo hello;whoami.
            // Group.Value returns only the last fragment, so use Captures to preserve the complete argument.
            return processArguments.Matches(arguments).Cast<Match>()
                .Select(match => string.Concat(match.Groups["text"].Captures.Cast<Capture>().Select(capture => capture.Value)));
        }

        private static IEnumerable<string> GetArgumentList(ProcessStartInfo startInfo)
        {
            return argumentListProperty == null
                ? Array.Empty<string>()
                : (IEnumerable<string>)argumentListProperty.GetValue(startInfo);
        }
    }
}
