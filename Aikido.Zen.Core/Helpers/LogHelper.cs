using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// Helper class for logging operations in the Aikido Zen system.
    /// </summary>
    public static class LogHelper
    {
        /// <summary>
        /// Logs a debug message if debugging is enabled.
        /// </summary>
        /// <param name="logger">The logger instance to use.</param>
        /// <param name="message">The message to log.</param>
        /// <summary>
        /// Logs a debug message if debugging is enabled, after sanitizing the message to prevent log injection.
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
        /// Sanitizes a log message to prevent log injection.
        /// </summary>
        /// <param name="message">The message to sanitize.</param>
        /// <returns>The sanitized message.</returns>
        public static string SanitizeMessage(string message)
        {
            // Replace any potentially dangerous characters or patterns
            return message.Replace("\r", "").Replace("\n", "").Replace("\t", "");
        }
    }
}
