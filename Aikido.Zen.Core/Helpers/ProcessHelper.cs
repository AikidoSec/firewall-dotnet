using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Aikido.Zen.Core.Helpers
{
    internal static class ProcessHelper
    {
        private static readonly HashSet<string> unixShells = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "sh", "bash", "zsh", "dash", "ksh", "fish", "tcsh", "csh"
        };
        private static readonly HashSet<string> powerShells = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "powershell", "pwsh" };
        private static readonly Regex powerShellSwitch = new Regex(
            @"^\s*(?:([-\u2013\u2014\u2015])\1?|/)(?:(?<command>c|co|com|comm|comma|comman|command)"
            + @"|f|fi|fil|file|cwa|commandwithargs)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex cmdSwitch = new Regex(@"(?:^|\s)/[ck]\s*", RegexOptions.IgnoreCase);
        private static readonly char[] pathSeparators = { '/', '\\' };

        // ArgumentList is not part of netstandard2.0.
        private static readonly PropertyInfo argumentListProperty = typeof(ProcessStartInfo).GetProperty("ArgumentList");

        internal static List<string> GetCommandsToInspect(ProcessStartInfo startInfo)
        {
            // Decoding can remove quotes belonging to user input that the detector matches verbatim.
            var commands = new List<string> { startInfo.FileName + " " + startInfo.Arguments };
            var program = GetProgramName(startInfo.FileName);
            string command;
            if (IsBatchFile(program) || string.Equals(program, "cmd", StringComparison.OrdinalIgnoreCase))
            {
                command = GetCmdCommand(startInfo);
            }
            else if (unixShells.Contains(program))
            {
                command = GetUnixShellCommand(GetArgv(startInfo));
            }
            else if (powerShells.Contains(program))
            {
                command = GetPowerShellCommand(GetArgv(startInfo));
            }
            else
            {
                return commands;
            }
            if (command != null)
            {
                commands.Add(command);
            }
            return commands;
        }

        private static string GetUnixShellCommand(List<string> argv)
        {
            for (int i = 1; i < argv.Count && argv[i] != "--"; i++)
            {
                var argument = argv[i];
                if (argument.Length > 1 && argument[0] == '-' && argument[1] != '-'
                    && argument.IndexOf('=') < 0 && argument.IndexOf('c', 1) > 0)
                {
                    // Arguments after the script are positional data ($0, $1, ...).
                    return i + 1 < argv.Count ? string.Join(" ", argv.Take(i + 2)) : null;
                }
            }
            return null;
        }

        private static string GetPowerShellCommand(List<string> argv)
        {
            // After -File or -CommandWithArgs, later switches can be script arguments.
            var commandSwitch = argv.Skip(1).Select(argument => powerShellSwitch.Match(argument)).FirstOrDefault(match => match.Success);
            return commandSwitch?.Groups["command"].Success == true ? string.Join(" ", argv) : null;
        }

        private static string GetCmdCommand(ProcessStartInfo startInfo)
        {
            var arguments = !string.IsNullOrEmpty(startInfo.Arguments)
                ? startInfo.Arguments
                : string.Join(" ", GetArgumentList(startInfo).Select(QuoteCmdArgument));
            bool batchFile = IsBatchFile(GetProgramName(startInfo.FileName));
            var commandSwitch = cmdSwitch.Match(arguments);
            int scriptStart = commandSwitch.Index + commandSwitch.Length;
            int lastQuote = arguments.LastIndexOf('"');

            // cmd removes the opening and last quote around its /c or /k command, leaving inner quotes intact.
            if (!batchFile && commandSwitch.Success && scriptStart < lastQuote && arguments[scriptStart] == '"')
            {
                arguments = arguments.Remove(lastQuote, 1).Remove(scriptStart, 1);
            }
            return startInfo.FileName + " " + arguments;
        }

        internal static List<string> GetArgv(ProcessStartInfo startInfo)
        {
            var arguments = string.IsNullOrEmpty(startInfo.Arguments)
                ? GetArgumentList(startInfo)
                : ParseArguments(startInfo.Arguments);
            return new[] { startInfo.FileName }.Concat(arguments).ToList();
        }

        internal static List<string> ParseArguments(string arguments)
        {
            var results = new List<string>();
            int position = 0;
            while (position < arguments.Length)
            {
                if (IsArgumentSeparator(arguments[position]))
                {
                    position++;
                    continue;
                }
                results.Add(ReadArgument(arguments, ref position));
            }
            return results;
        }

        private static string ReadArgument(string arguments, ref int position)
        {
            var result = new StringBuilder();
            bool inQuotes = false;
            while (position < arguments.Length)
            {
                char c = arguments[position];
                if (!inQuotes && IsArgumentSeparator(c))
                {
                    break;
                }
                position++;
                if (c == '\\')
                {
                    int backslashes = 1;
                    while (position < arguments.Length && arguments[position] == '\\')
                    {
                        backslashes++;
                        position++;
                    }
                    bool beforeQuote = position < arguments.Length && arguments[position] == '"';
                    result.Append('\\', beforeQuote ? backslashes / 2 : backslashes);
                    if (beforeQuote && backslashes % 2 != 0)
                    {
                        result.Append('"');
                        position++;
                    }
                }
                else if (c == '"')
                {
                    if (inQuotes && position < arguments.Length && arguments[position] == '"')
                    {
                        result.Append('"');
                        position++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else
                {
                    result.Append(c);
                }
            }
            return result.ToString();
        }

        private static bool IsArgumentSeparator(char c)
        {
            // Process.Start only treats space and tab as separators; a newline belongs to the argument.
            return c == ' ' || c == '\t';
        }

        private static string QuoteCmdArgument(string argument)
        {
            bool needsQuotes = string.IsNullOrEmpty(argument) || argument.Any(c => char.IsWhiteSpace(c) || c == '"');
            return needsQuotes ? "\"" + argument + "\"" : argument;
        }

        private static IEnumerable<string> GetArgumentList(ProcessStartInfo startInfo)
        {
            return argumentListProperty == null
                ? Array.Empty<string>()
                : (IEnumerable<string>)argumentListProperty.GetValue(startInfo);
        }

        private static bool IsBatchFile(string program)
        {
            return program.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)
                || program.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetProgramName(string program)
        {
            // Path methods throw on invalid path characters in .NET Framework.
            var name = program.Substring(program.LastIndexOfAny(pathSeparators) + 1);
            return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name.Substring(0, name.Length - 4) : name;
        }
    }
}
