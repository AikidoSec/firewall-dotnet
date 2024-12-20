namespace Aikido.Zen.Core.Vulnerabilities
{

    /// <summary>
    /// Detector for shell injection vulnerabilities in command strings
    /// </summary>
    public class ShellInjectionDetector
    {
        /// <summary>
        /// Detects potential shell injection vulnerabilities in a command string
        /// </summary>
        /// <param name="command">The shell command to analyze</param>
        /// <param name="userInput">The user input to check for injection attempts</param>
        /// <returns>True if shell injection is detected, false otherwise</returns>
        public static bool IsShellInjection(string command, string userInput)
        {
            return ZenInternals.IsShellInjection(command, userInput);
        }
    }
}
