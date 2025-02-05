using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aikido.Zen.Core.Exceptions
{
    public class AikidoException : Exception
    {
        private const string _defaultMessage = "Unknown threat blocked";
        private static ILogger _logger = NullLogger.Instance;

        /// <summary>
        /// Configures a static logger for AikidoException.
        /// If not configured, uses NullLogger which safely does nothing.
        /// </summary>
        /// <param name="logger">The logger instance to use</param>
        public static void ConfigureLogger(ILogger logger)
        {
            _logger = logger ?? NullLogger.Instance;
        }

        public AikidoException(string message = _defaultMessage) : base(!string.IsNullOrEmpty(message) ? message : _defaultMessage)
        {
            _logger.LogError("Aikido security exception: {Message}", Message);
        }

        public static AikidoException SQLInjectionDetected(string dialect)
        {
            return new AikidoException($"{dialect}: SQL injection detected");
        }

        public static AikidoException NoSQLInjectionDetected ()
        {
            return new AikidoException("NoSQL injection detected");
        }

        public static AikidoException ShellInjectionDetected()
        {
            return new AikidoException($"Shell injection detected");
        }

        public static AikidoException RequestBlocked(string route)
        {
            return new AikidoException($"Request blocked: {route}");
        }

        public static AikidoException RateLimited(string route)
        {
            return new AikidoException($"Ratelimited: {route}");
        }

        public static AikidoException PathTraversalDetected(string assemblyName, string operation)
        {
            return new AikidoException($"Path traversal detected in {assemblyName} during {operation}");
        }
    }
}
