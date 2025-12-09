using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Text;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Core.Vulnerabilities
{
    /// <summary>
    /// Internal utilities for detecting security vulnerabilities like SQL injection and shell injection
    /// </summary>
    internal static partial class ZenInternals
    {


        private static byte[] UTF8Bytes(string str)
        {
            return Encoding.UTF8.GetBytes(str);
        }

        [DllImport("libraries/libzen_internals_x86_64-pc-windows-gnu.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "detect_sql_injection")]
        private static extern int detect_sql_injection_windows_x86_64(
            [In] byte[] query,
            UIntPtr query_length,
            [In] byte[] userinput,
            UIntPtr userinput_length,
            int dialect);

        [DllImport("libraries/libzen_internals_aarch64-pc-windows-msvc.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "detect_sql_injection")]
        private static extern int detect_sql_injection_windows_arm64(
            [In] byte[] query,
            UIntPtr query_length,
            [In] byte[] userinput,
            UIntPtr userinput_length,
            int dialect);

        [DllImport("libraries/libzen_internals_aarch64-apple-darwin.dylib", CallingConvention = CallingConvention.Cdecl, EntryPoint = "detect_sql_injection")]
        private static extern int detect_sql_injection_osx_arm64(
            [In] byte[] query,
            UIntPtr query_length,
            [In] byte[] userinput,
            UIntPtr userinput_length,
            int dialect);

        [DllImport("libraries/libzen_internals_x86_64-apple-darwin.dylib", CallingConvention = CallingConvention.Cdecl, EntryPoint = "detect_sql_injection")]
        private static extern int detect_sql_injection_osx_x86_64(
            [In] byte[] query,
            UIntPtr query_length,
            [In] byte[] userinput,
            UIntPtr userinput_length,
            int dialect);

        [DllImport("libraries/libzen_internals_aarch64-unknown-linux-gnu.so", CallingConvention = CallingConvention.Cdecl, EntryPoint = "detect_sql_injection")]
        private static extern int detect_sql_injection_linux_arm64(
            [In] byte[] query,
            UIntPtr query_length,
            [In] byte[] userinput,
            UIntPtr userinput_length,
            int dialect);

        [DllImport("libraries/libzen_internals_x86_64-unknown-linux-gnu.so", CallingConvention = CallingConvention.Cdecl, EntryPoint = "detect_sql_injection")]
        private static extern int detect_sql_injection_linux_x86_64(
            [In] byte[] query,
            UIntPtr query_length,
            [In] byte[] userinput,
            UIntPtr userinput_length,
            int dialect);

        /// <summary>
        /// Detects potential SQL injection vulnerabilities in a query string
        /// </summary>
        /// <param name="query">The SQL query to analyze</param>
        /// <param name="userInput">The user input to check for injection attempts</param>
        /// <param name="dialect">The SQL dialect identifier</param>
        /// <returns>True if SQL injection is detected, false otherwise. Returns false on errors or tokenization failures.</returns>
        internal static bool IsSQLInjection(string query, string userInput, int dialect)
        {
            // Some quick checks are cheaper than calling the Rust library
            if (ShouldReturnEarly(query, userInput))
            {
                return false;
            }

            var queryBytes = UTF8Bytes(query);
            var userInputBytes = UTF8Bytes(userInput);

            int result;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                {
                    result = detect_sql_injection_windows_arm64(
                        queryBytes,
                        (UIntPtr)queryBytes.Length,
                        userInputBytes,
                        (UIntPtr)userInputBytes.Length,
                        dialect);
                }
                else
                {
                    result = detect_sql_injection_windows_x86_64(
                        queryBytes,
                        (UIntPtr)queryBytes.Length,
                        userInputBytes,
                        (UIntPtr)userInputBytes.Length,
                        dialect);
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // check if arm64 or x86_64
                if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                {
                    result = detect_sql_injection_osx_arm64(
                        queryBytes,
                        (UIntPtr)queryBytes.Length,
                        userInputBytes,
                        (UIntPtr)userInputBytes.Length,
                        dialect);
                }
                else
                {
                    result = detect_sql_injection_osx_x86_64(
                        queryBytes,
                        (UIntPtr)queryBytes.Length,
                        userInputBytes,
                        (UIntPtr)userInputBytes.Length,
                        dialect);
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // check if arm64 or x86_64
                if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                {
                    result = detect_sql_injection_linux_arm64(
                        queryBytes,
                        (UIntPtr)queryBytes.Length,
                        userInputBytes,
                        (UIntPtr)userInputBytes.Length,
                        dialect);
                }
                else
                {
                    result = detect_sql_injection_linux_x86_64(
                        queryBytes,
                        (UIntPtr)queryBytes.Length,
                        userInputBytes,
                        (UIntPtr)userInputBytes.Length,
                        dialect);
                }
            }
            else
            {
                throw new PlatformNotSupportedException("Unsupported platform");
            }

            switch (result)
            {
                case 0:
                    return false; // return code 0 comes from zen-internals to indicate that there was no sql injection
                case 1:
                    return true; // return code 1 comes from zen-internals to indicate that there was a sql injection detected
                case 2:
                    LogHelper.ErrorLog(Agent.Logger, "Error in detecting SQL injection: internal error");
                    return false;
                case 3:
                    // return code 3 is what zen-internals returns when the sql tokenization has failed, we don't block this.
                    return false;
                default:
                    LogHelper.ErrorLog(Agent.Logger, $"Unexpected result from SQL injection detection: {result}");
                    return false;
            }
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
            if (query == null || userInput == null || userInput.Length <= 1 || query.Length < userInput.Length)
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
