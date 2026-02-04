using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

[assembly: InternalsVisibleTo("Aikido.Zen.Tests")]
namespace Aikido.Zen.Core.Vulnerabilities
{

    /// <summary>
    /// Detector for shell injection vulnerabilities in command strings
    /// </summary>
    public class ShellInjectionDetector
    {
        private static readonly string[] pathPrefixes = { "/bin/", "/sbin/", "/usr/bin/", "/usr/sbin/", "/usr/local/bin/", "/usr/local/sbin/" };
        private static readonly char[] separators = { ' ', '\t', '\n', ';', '&', '|', '(', ')', '<', '>' };

        // Define dangerous characters and commands as static fields
        private static readonly char[] dangerousChars = { '#', '!', '"', '$', '&', '\'', '(', ')', '*', ';', '<', '=', '>', '?', '[', '\\', ']', '^', '`', '{', '|', '}', ' ', '\n', '\t', '~' };
        private static readonly string[] dangerousCommands = { "sleep", "shutdown", "reboot", "poweroff", "halt", "ifconfig", "chmod", "chown", "ping", "ssh", "scp", "curl", "wget", "telnet", "kill", "killall", "rm", "mv", "cp", "touch", "echo", "cat", "head", "tail", "grep", "find", "awk", "sed", "sort", "uniq", "wc", "ls", "env", "ps", "who", "whoami", "id", "w", "df", "du", "pwd", "uname", "hostname", "netstat", "passwd", "arch", "printenv", "logname", "pstree", "hostnamectl", "set", "lsattr", "killall5", "dmesg", "history", "free", "uptime", "finger", "top", "shopt", ":" };
        private static readonly char[] dangerousCharsInsideDoubleQuotes = { '$', '`', '\\', '!' };

        private static readonly Regex commandsRegex = new Regex(
            $"([/.]*({string.Join("|", pathPrefixes.Select(Regex.Escape))})?({string.Join("|", dangerousCommands.OrderByDescending(c => c.Length))}))",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

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
                if (userInput != match.Value)
                {
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
        /// Checks if the user input is safely encapsulated within quotes.
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
