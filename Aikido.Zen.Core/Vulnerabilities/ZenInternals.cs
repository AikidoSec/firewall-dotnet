using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Text;

namespace Aikido.Zen.Core.Vulnerabilities
{
    /// <summary>
    /// Internal utilities for detecting security vulnerabilities like SQL injection and shell injection
    /// </summary>
    internal static partial class ZenInternals
    {


        private static byte[] NullTerminatedUTF8bytes(string str)
        {
            return Encoding.UTF8.GetBytes(str + "\0");
        }

        [DllImport("libraries/libzen_internals_x86_64-pc-windows-gnu.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "detect_shell_injection")]
        private static extern int detect_shell_injection_windows(
            [In] byte[] command,
            [In] byte[] userinput);

        [DllImport("libraries/libzen_internals_aarch64-apple-darwin.dylib", CallingConvention = CallingConvention.Cdecl, EntryPoint = "detect_shell_injection")]
        private static extern int detect_shell_injection_osx_arm64(
            [In] byte[] command,
            [In] byte[] userinput);

        [DllImport("libraries/libzen_internals_x86_64-apple-darwin.dylib", CallingConvention = CallingConvention.Cdecl, EntryPoint = "detect_shell_injection")]
        private static extern int detect_shell_injection_osx_x86_64(
            [In] byte[] command,
            [In] byte[] userinput);

        [DllImport("libraries/libzen_internals_aarch64-unknown-linux-gnu.so", CallingConvention = CallingConvention.Cdecl, EntryPoint = "detect_shell_injection")]
        private static extern int detect_shell_injection_linux_arm64(
            [In] byte[] command,
            [In] byte[] userinput);

        [DllImport("libraries/libzen_internals_x86_64-unknown-linux-gnu.so", CallingConvention = CallingConvention.Cdecl, EntryPoint = "detect_shell_injection")]
        private static extern int detect_shell_injection_linux_x86_64(
            [In] byte[] command,
            [In] byte[] userinput);

        [DllImport("libraries/libzen_internals_x86_64-pc-windows-gnu.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "detect_sql_injection")]
        private static extern int detect_sql_injection_windows(
            [In] byte[] query,
            [In] byte[] userinput,
            int dialect);

        [DllImport("libraries/libzen_internals_aarch64-apple-darwin.dylib", CallingConvention = CallingConvention.Cdecl, EntryPoint = "detect_sql_injection")]
        private static extern int detect_sql_injection_osx_arm64(
            [In] byte[] query,
            [In] byte[] userinput,
            int dialect);

        [DllImport("libraries/libzen_internals_x86_64-apple-darwin.dylib", CallingConvention = CallingConvention.Cdecl, EntryPoint = "detect_sql_injection")]
        private static extern int detect_sql_injection_osx_x86_64(
            [In] byte[] query,
            [In] byte[] userinput,
            int dialect);

        [DllImport("libraries/libzen_internals_aarch64-unknown-linux-gnu.so", CallingConvention = CallingConvention.Cdecl, EntryPoint = "detect_sql_injection")]
        private static extern int detect_sql_injection_linux_arm64(
            [In] byte[] query,
            [In] byte[] userinput,
            int dialect);

        [DllImport("libraries/libzen_internals_x86_64-unknown-linux-gnu.so", CallingConvention = CallingConvention.Cdecl, EntryPoint = "detect_sql_injection")]
        private static extern int detect_sql_injection_linux_x86_64(
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
            int result;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                result = detect_shell_injection_windows(
                    NullTerminatedUTF8bytes(command),
                    NullTerminatedUTF8bytes(userInput));
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // check if arm64 or x86_64
                if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                {
                    result = detect_shell_injection_osx_arm64(
                        NullTerminatedUTF8bytes(command),
                        NullTerminatedUTF8bytes(userInput));
                }
                else
                {
                    result = detect_shell_injection_osx_x86_64(
                        NullTerminatedUTF8bytes(command),
                        NullTerminatedUTF8bytes(userInput));
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // check if arm64 or x86_64
                if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                {
                    result = detect_shell_injection_linux_arm64(
                        NullTerminatedUTF8bytes(command),
                        NullTerminatedUTF8bytes(userInput));
                }
                else
                {
                    result = detect_shell_injection_linux_x86_64(
                        NullTerminatedUTF8bytes(command),
                        NullTerminatedUTF8bytes(userInput));
                }
            }
            else
            {
                throw new PlatformNotSupportedException("Unsupported platform");
            }

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

            int result;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                result = detect_sql_injection_windows(
                    NullTerminatedUTF8bytes(query),
                    NullTerminatedUTF8bytes(userInput),
                    dialect);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // check if arm64 or x86_64
                if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                {
                    result = detect_sql_injection_osx_arm64(
                        NullTerminatedUTF8bytes(query),
                        NullTerminatedUTF8bytes(userInput),
                        dialect);
                }
                else
                {
                    result = detect_sql_injection_osx_x86_64(
                        NullTerminatedUTF8bytes(query),
                        NullTerminatedUTF8bytes(userInput),
                        dialect);
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // check if arm64 or x86_64
                if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                {
                    result = detect_sql_injection_linux_arm64(
                        NullTerminatedUTF8bytes(query),
                        NullTerminatedUTF8bytes(userInput),
                        dialect);
                }
                else
                {
                    result = detect_sql_injection_linux_x86_64(
                        NullTerminatedUTF8bytes(query),
                        NullTerminatedUTF8bytes(userInput),
                        dialect);
                }
            }
            else
            {
                throw new PlatformNotSupportedException("Unsupported platform");
            }

            if (result > 1)
            {
                throw new Exception("Error in detecting SQL injection");
            }
            return result == 1;
        }

        /// <summary>
        /// Determines if the SQL injection detection algorithm should return early
        /// </summary>
        /// <param name="query">The SQL query to analyze (lowercased)</param>
        /// <param name="userInput">The user input to check for injection attempts (lowercased)</param>
        /// <returns>True if the detection algorithm should return early, false otherwise</returns>
        private static bool ShouldReturnEarly(string query, string userInput)
        {
            // user_input is <= 1 char or user input larger than query
            // e.g. user_input = "" and query = "SELECT * FROM users"
            if (userInput.Length <= 1 || query.Length < userInput.Length)
            {
                return true;
            }

            // user_input not in query
            // e.g. user_input = "admin" and query = "SELECT * FROM users"
            if (!query.Contains(userInput))
            {
                return true;
            }

            // user_input is alphanumerical
            // e.g. user_input = "admin" and query = "SELECT * FROM users"
            if (Regex.IsMatch(userInput.Replace("_", ""), @"^[a-zA-Z0-9_]+$"))
            {
                return true;
            }

            // user_input is an array of integers
            // e.g. user_input = "[1, 2, 3]" and query = "SELECT * FROM users"
            string cleanedInputForList = userInput.Replace(" ", "").Replace(",", "");
            return Regex.IsMatch(cleanedInputForList, @"^\d+$");
        }
    }
}
