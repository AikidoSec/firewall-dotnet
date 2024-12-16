using System.Runtime.InteropServices;
using System;
using System.Text.RegularExpressions;
using System.Text;

namespace Aikido.Zen.Core.Vulnerabilities
{
    /// <summary>
    /// Internal utilities for detecting security vulnerabilities like SQL injection and shell injection
    /// </summary>
    internal static partial class ZenInternals
    {
        internal const string RustLibraryPath = "libraries/libzen_internals.dll";

        private static byte[] NullTerminatedUTF8bytes(string str)
        {
            return Encoding.UTF8.GetBytes(str + "\0");
        }

        [DllImport(RustLibraryPath, CallingConvention = CallingConvention.Cdecl, EntryPoint = "detect_shell_injection")]
        private static extern int detect_shell_injection(
            [In] byte[] command,
            [In] byte[] userinput);

        [DllImport(RustLibraryPath, CallingConvention = CallingConvention.Cdecl, EntryPoint = "detect_sql_injection")]
        private static extern int detect_sql_injection(
            [In] byte[] query,
            [In] byte[] userinput,
            int dialect);

        /// <summary>
        /// Detects potential shell injection vulnerabilities in a command string
        /// </summary>
        /// <param name="command">The shell command to analyze</param>
        /// <param name="userInput">The user input to check for injection attempts</param>
        /// <returns>True if shell injection is detected, false otherwise</returns>
        /// <exception cref="Exception">Thrown when there is an error in the detection process</exception>
        internal static bool IsShellInjection(string command, string userInput)
        {
            var result = detect_shell_injection(
                NullTerminatedUTF8bytes(command),
                NullTerminatedUTF8bytes(userInput));
            if (result > 1)
            {
                throw new Exception("Error in detecting shell injection");
            }
            return result == 1;
        }

        /// <summary>
        /// Detects potential SQL injection vulnerabilities in a query string
        /// </summary>
        /// <param name="query">The SQL query to analyze</param>
        /// <param name="userInput">The user input to check for injection attempts</param>
        /// <param name="dialect">The SQL dialect identifier</param>
        /// <returns>True if SQL injection is detected, false otherwise</returns>
        /// <exception cref="Exception">Thrown when there is an error in the detection process</exception>
        internal static bool IsSQLInjection(string query, string userInput, int dialect)
        {
            // Some quick checks are cheaper than calling the Rust library
            if (ShouldReturnEarly(query, userInput))
            {
                return false;
            }
            var result = detect_sql_injection(
                NullTerminatedUTF8bytes(query),
                NullTerminatedUTF8bytes(userInput),
                dialect);
            if (result > 1)
            {
                throw new Exception("Error in detecting shell injection");
            }
            return result == 1;
        }

        private static bool ShouldReturnEarly(string query, string userInput)
        {
            if (query == null || userInput == null || userInput.Length <= 1 || query.Length < userInput.Length)
            {
                return true;
            }

            if (!query.Contains(userInput))
            {
                return true;
            }

            if (Regex.IsMatch(userInput.Replace("_", ""), @"^[a-zA-Z0-9_]+$"))
            {
                return true;
            }

            string cleanedInputForList = userInput.Replace(" ", "").Replace(",", "");
            return Regex.IsMatch(cleanedInputForList, @"^\d+$");
        }
    }
}
