using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Aikido.Zen.Core.Helpers;

[assembly: InternalsVisibleTo("Aikido.Zen.Tests")]
namespace Aikido.Zen.Core.Vulnerabilities
{

    /// <summary>
    /// Detector for shell injection vulnerabilities in command strings
    /// </summary>
    public class ShellInjectionDetector
    {
        private static readonly string[] pathPrefixes = { "/bin/", "/sbin/", "/usr/bin/", "/usr/sbin/", "/usr/local/bin/", "/usr/local/sbin/" };
        private static readonly char[] separators = { ' ', '\t', '\n', ';', '&', '|', '(', ')', '<', '>', '\r', '\f' };

        // Define dangerous characters and commands as static fields
        private static readonly char[] dangerousChars = { '#', '!', '"', '$', '&', '\'', '(', ')', '*', ';', '<', '=', '>', '?', '[', '\\', ']', '^', '`', '{', '|', '}', ' ', '\n', '\t', '~', '\r', '\f' };
        private static readonly string[] dangerousCommands = { "sleep", "shutdown", "reboot", "poweroff", "halt", "ifconfig", "chmod", "chown", "ping", "ssh", "scp", "curl", "wget", "telnet", "kill", "killall", "rm", "mv", "cp", "touch", "echo", "cat", "head", "tail", "grep", "find", "awk", "sed", "sort", "uniq", "wc", "ls", "env", "ps", "who", "whoami", "id", "w", "df", "du", "pwd", "uname", "hostname", "netstat", "passwd", "arch", "printenv", "logname", "pstree", "hostnamectl", "set", "lsattr", "killall5", "dmesg", "history", "free", "uptime", "finger", "top", "shopt", ":" };
        private static readonly char[] dangerousCharsInsideDoubleQuotes = { '$', '`', '\\', '!' };

        private static readonly Regex commandsRegex = new Regex(
            $"([/.]*({string.Join("|", pathPrefixes.Select(Regex.Escape))})?({string.Join("|", dangerousCommands.OrderByDescending(c => c.Length))}))",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        private static readonly HashSet<string> unixShells = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sh", "bash", "zsh" };
        private static readonly HashSet<string> powerShells = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "powershell", "pwsh" };
        private static readonly Regex unixCommandSwitch = new Regex(@"\A-[el]*c[el]*\z");
        // PowerShell accepts abbreviated switches, surrounding whitespace and these Unicode dashes.
        private static readonly Regex powerShellSwitch = new Regex(
            @"\A\s*(?:([-\u2013\u2014\u2015])\1?|/)(?:(?<command>c|co|com|comm|comma|comman|command)"
            + @"|(?<startup>NoProfile|NoLogo|NonInteractive|NoExit|Sta|Mta)|(?<policy>ExecutionPolicy|ep))\s*\z",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex quotedCmdCommand = new Regex(
            @"\A(?<prefix>[ \t]*(?:/[ds][ \t]+)*/[ck][ \t]*)""(?<command>.*)""",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly char[] pathSeparators = { '/', '\\' };

        /// <summary>
        /// Detects potential shell injection vulnerabilities in a command string
        /// </summary>
        /// <param name="command">The shell command to analyze</param>
        /// <param name="userInput">The user input to check for injection attempts</param>
        /// <returns>True if shell injection is detected, false otherwise</returns>
        public static bool IsShellInjection(string command, string userInput)
        {
            // Block single ~ character
            if (userInput == "~" && command.Length > 1 && command.Contains("~"))
            {
                return true;
            }

            if (userInput.Length <= 1 || userInput.Length > command.Length || !command.Contains(userInput))
            {
                return false;
            }

            if (IsSafelyEncapsulated(command, userInput))
            {
                return false;
            }

            return ContainsShellSyntax(command, userInput);
        }

        internal static string[] GetCommandsToInspect(ProcessStartInfo startInfo)
        {
            // Decoding can remove quotes belonging to user input that the detector matches verbatim.
            return new[] { startInfo.FileName + " " + startInfo.Arguments, GetShellCommand(startInfo) };
        }

        // Shells execute command arguments as code: sh -c "echo ok;id" executes echo ok;id.
        // Only normalize supported direct launches; script arguments and nested shells need different rules.
        internal static string GetShellCommand(ProcessStartInfo startInfo)
        {
            var program = GetProgramName(startInfo.FileName);
            string command;
            if (IsBatchFile(program) || string.Equals(program, "cmd", StringComparison.OrdinalIgnoreCase))
            {
                command = GetCmdCommand(startInfo);
            }
            else if (unixShells.Contains(program))
            {
                command = GetUnixShellCommand(ProcessHelper.GetArguments(startInfo));
            }
            else if (powerShells.Contains(program))
            {
                command = GetPowerShellCommand(ProcessHelper.GetArguments(startInfo));
            }
            else
            {
                return null;
            }
            return command == null ? null : startInfo.FileName + " " + command;
        }

        private static string GetUnixShellCommand(List<string> arguments)
        {
            int commandIndex = arguments.TakeWhile(arg => arg == "-e" || arg == "-l").Count();
            // Arguments after the script are positional data ($0, $1, ...).
            return commandIndex + 1 < arguments.Count && unixCommandSwitch.IsMatch(arguments[commandIndex])
                ? string.Join(" ", arguments.Take(commandIndex + 2)) : null;
        }

        private static string GetPowerShellCommand(List<string> arguments)
        {
            int commandIndex = arguments.TakeWhile(arg => powerShellSwitch.Match(arg).Groups["startup"].Success).Count();
            if (commandIndex + 1 < arguments.Count && powerShellSwitch.Match(arguments[commandIndex]).Groups["policy"].Success
                && string.Equals(arguments[commandIndex + 1], "Bypass", StringComparison.OrdinalIgnoreCase))
            {
                commandIndex += 2;
                commandIndex += arguments.Skip(commandIndex).TakeWhile(arg => powerShellSwitch.Match(arg).Groups["startup"].Success).Count();
            }
            return commandIndex + 1 < arguments.Count && powerShellSwitch.Match(arguments[commandIndex]).Groups["command"].Success
                ? string.Join(" ", arguments) : null;
        }

        private static string GetCmdCommand(ProcessStartInfo startInfo)
        {
            var arguments = !string.IsNullOrEmpty(startInfo.Arguments)
                ? startInfo.Arguments
                : string.Join(" ", ProcessHelper.GetArguments(startInfo).Select(QuoteCmdArgument));
            // cmd removes the opening and last quote around its /c or /k command, leaving inner quotes intact.
            // For example, /c "echo "Tom & Jerry"" becomes /c echo "Tom & Jerry", where & is still quoted.
            if (!IsBatchFile(GetProgramName(startInfo.FileName)))
            {
                arguments = quotedCmdCommand.Replace(arguments, "${prefix}${command}");
            }
            return arguments;
        }

        private static string QuoteCmdArgument(string argument)
        {
            bool needsQuotes = string.IsNullOrEmpty(argument) || argument.Any(c => char.IsWhiteSpace(c) || c == '"');
            return needsQuotes ? "\"" + argument + "\"" : argument;
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

        /// <summary>
        /// Checks if the user input contains shell syntax that could lead to injection.
        /// </summary>
        /// <param name="command">The shell command to analyze</param>
        /// <param name="userInput">The user input to check</param>
        /// <returns>True if dangerous shell syntax is found, false otherwise</returns>
        private static bool ContainsShellSyntax(string command, string userInput)
        {
            if (string.IsNullOrWhiteSpace(userInput))
            {
                return false;
            }

            // Check for dangerous characters
            if (userInput.Any(c => dangerousChars.Contains(c)))
            {
                return true;
            }

            // The command is the same as the user input
            if (command == userInput)
            {
                var match = commandsRegex.Match(command);
                return match.Success && match.Index == 0 && match.Length == command.Length;
            }

            // Check if the command contains a commonly used command
            foreach (Match match in commandsRegex.Matches(command))
            {
                if (!userInput.Contains(match.Value))
                {
                    // Don't skip whoami%00
                    continue;
                }

                ReadOnlySpan<char> commandSpan = command.AsSpan();
                char charBefore = match.Index > 0 ? commandSpan[match.Index - 1] : '\0';
                char charAfter = match.Index + match.Length < commandSpan.Length ? commandSpan[match.Index + match.Length] : '\0';

                if (separators.Contains(charBefore) && separators.Contains(charAfter))
                {
                    return true;
                }

                if (separators.Contains(charBefore) && charAfter == '\0')
                {
                    return true;
                }

                if (charBefore == '\0' && separators.Contains(charAfter))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks quoting around user input in the supplied command text. Process-argument quotes can
        /// look identical to shell quotes, so supported shell launches are also inspected after decoding.
        /// </summary>
        /// <param name="command">The shell command to analyze</param>
        /// <param name="userInput">The user input to check</param>
        /// <returns>True if the input is safely encapsulated, false otherwise</returns>
        internal static bool IsSafelyEncapsulated(string command, string userInput)
        {
            // Split the command by the user input to get segments before and after the user input
            var segments = command.Split(new[] { userInput }, StringSplitOptions.None);

            // Iterate over each segment pair to check encapsulation
            for (int i = 0; i < segments.Length - 1; i++)
            {
                string currentSegment = segments[i];
                string nextSegment = segments[i + 1];

                // Get the character before and after the user input
                char charBeforeUserInput = currentSegment.Length > 0 ? currentSegment[currentSegment.Length - 1] : '\0';
                char charAfterUserInput = nextSegment.Length > 0 ? nextSegment[0] : '\0';

                // Check if the character before the user input is an escape character
                bool isEscapeChar = charBeforeUserInput == '"' || charBeforeUserInput == '\'';

                if (!isEscapeChar)
                {
                    return false;
                }

                // Check if the character before and after the user input are the same
                if (charBeforeUserInput != charAfterUserInput)
                {
                    return false;
                }

                // Check if the user input contains the escape character
                if (userInput.IndexOf(charBeforeUserInput) != -1)
                {
                    return false;
                }

                // Check for dangerous characters inside double quotes
                if (charBeforeUserInput == '"' && userInput.IndexOfAny(dangerousCharsInsideDoubleQuotes) != -1)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
