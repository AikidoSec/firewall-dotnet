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
        public static void DebugLog(ILogger logger, string message)
        {
            if (EnvironmentHelper.IsDebugging)
            {
                logger.LogDebug(message);
                Debug.WriteLine(message);
            }
        }
    }
}
