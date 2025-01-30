using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// Configures logging for the Aikido Zen system, allowing for console logging and additional custom configurations.
    /// </summary>
    public static class LoggerConfigurator
    {
        private static ILoggerFactory _loggerFactory = DefaultFactory();

        /// <summary>
        /// Configures logging with additional custom configurations using LoggerFactory.
        /// </summary>
        /// <param name="configure">An action to configure additional logging providers directly on the LoggerFactory.</param>
        public static void ConfigureLogging (Func<ILoggerFactory> loggerFactoryProvider = null)
        {
            _loggerFactory = loggerFactoryProvider == null
                ? DefaultFactory()
                : loggerFactoryProvider.Invoke();
        }

        /// <summary>
        /// Creates a logger for the specified type.
        /// </summary>
        /// <typeparam name="T">The type for which to create a logger.</typeparam>
        /// <returns>An ILogger instance for the specified type.</returns>
        internal static ILogger<T> CreateLogger<T> ()
        {

            return _loggerFactory.CreateLogger<T>();
        }

        public static ILoggerFactory DefaultFactory()
        {
            return LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                // allow debug logging to be enabled if the environment variable is set
                if (EnvironmentHelper.IsDebugging)
                {
                    // debug logs and up from the Agent class should be written to the console
                    builder.AddFilter("Aikido.Zen.Core.Models.Agent", LogLevel.Debug);
                }
                else
                {
                    // only informational logs and up from the Agent class should be written to the console
                    builder.AddFilter("Aikido.Zen.Core.Models.Agent", LogLevel.Information);
                }
            });
        }
    }
}
