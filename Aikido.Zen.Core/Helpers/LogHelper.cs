using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// Helper class for logging operations in the Aikido Zen system.
    /// </summary>
    public static class LogHelper
    {
        private static readonly int MaxLogs = 1000;
        private static readonly TimeSpan LogTimeSpan = TimeSpan.FromMinutes(60);
        private static readonly Queue<DateTime> _logTimestamps = new Queue<DateTime>();
        private static readonly object _logLock = new object();

        private static bool ShouldLog()
        {
            lock (_logLock)
            {
                // Remove timestamps older than the timespan
                while (_logTimestamps.Count > 0 && _logTimestamps.Peek() < DateTime.UtcNow - LogTimeSpan)
                {
                    _logTimestamps.Dequeue();
                }

                // Check if we've exceeded the log limit
                if (_logTimestamps.Count >= MaxLogs)
                {
                    return false; // Rate limit exceeded
                }

                _logTimestamps.Enqueue(DateTime.UtcNow);
                return true;
            }
        }

        /// <summary>
        /// Logs a debug message if debugging is enabled, after sanitizing the message and applying rate limiting.
        /// </summary>
        /// <param name="logger">The logger instance to use.</param>
        /// <param name="message">The message to log.</param>
        public static void DebugLog(ILogger logger, string message)
        {
            if (EnvironmentHelper.IsDebugging)
            {
                // Sanitize the message to prevent log injection
                string sanitizedMessage = SanitizeMessage(message);
                // we log the message to the outputs defined by the application
                logger.LogDebug(sanitizedMessage);
                // we also log the message to the debug output in case the application is running in a debugger
                Debug.WriteLine(sanitizedMessage);
            }
        }

        /// <summary>
        /// Logs an error message, after sanitizing the message and applying rate limiting.
        /// </summary>
        /// <param name="logger">The logger instance to use.</param>
        /// <param name="message">The message to log.</param>
        public static void ErrorLog(ILogger logger, string message) => ErrorLog(logger, null, message);

        /// <summary>
        /// Logs an error message, after sanitizing the message and applying rate limiting.
        /// </summary>
        /// <param name="logger">The logger instance to use.</param>
        /// <param name="exception">The exception associated with the error, if any.</param>
        /// <param name="message">The message to log.</param>
        public static void ErrorLog(ILogger logger, Exception exception, string message)
        {
            // Sanitize the message to prevent log injection
            string sanitizedMessage = SanitizeMessage(message);

            if (exception == null)
            {
                // we log the message to the outputs defined by the application
                logger.LogError(sanitizedMessage);
            }
            else
            {
                // we log the message to the outputs defined by the application
                logger.LogError(exception, sanitizedMessage);
            }
        }

        /// <summary>
        /// Logs an information message, after sanitizing the message and applying rate limiting.
        /// </summary>
        /// <param name="logger">The logger instance to use.</param>
        /// <param name="message">The message to log.</param>
        public static void InfoLog(ILogger logger, string message)
        {
            // Sanitize the message to prevent log injection
            string sanitizedMessage = SanitizeMessage(message);
            // we log the message to the outputs defined by the application
            logger.LogInformation(sanitizedMessage);
            // we also log the message to the debug output in case the application is running in a debugger
            Debug.WriteLine(sanitizedMessage);
        }

        /// <summary>
        /// Logs an attack-related information message, after sanitizing the message and applying rate limiting.
        /// This method wraps the InfoLog method.
        /// </summary>
        /// <param name="logger">The logger instance to use.</param>
        /// <param name="message">The message to log, typically detailing an attack attempt or security event.</param>
        public static void AttackLog(ILogger logger, string message)
        {
            if (ShouldLog())
            {
                InfoLog(logger, message);
            }
        }

        /// <summary>
        /// Sanitizes a log message to prevent log injection.
        /// </summary>
        /// <param name="message">The message to sanitize.</param>
        /// <returns>The sanitized message.</returns>
        public static string SanitizeMessage(string message)
        {
            // if log does not start with "AIKIDO: " then add it
            if (!message.StartsWith("AIKIDO: "))
            {
                message = $"AIKIDO: {message}";
            }
            // Replace any potentially dangerous characters or patterns
            return message.Replace("\r", "").Replace("\n", "").Replace("\t", "");
        }

        // mainly used for testing
        internal static void ClearQueue()
        {
            lock (_logLock)
            {
                _logTimestamps.Clear();
            }
        }
    }
}
