using System;
using Aikido.Zen.Core.Models;
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

        public static AikidoException Blocked(AttackKind kind, string operation)
        {
            switch (kind)
            {
                case AttackKind.SqlInjection:
                    return new AikidoException($"Zen has blocked an SQL injection during {operation}");

                case AttackKind.ShellInjection:
                    return new AikidoException($"Zen has blocked a shell injection during {operation}");

                case AttackKind.PathTraversal:
                    return new AikidoException($"Zen has blocked a path traversal attack during {operation}");

                case AttackKind.Ssrf:
                    return new AikidoException($"Zen has blocked a server-side request forgery during {operation}");

                case AttackKind.StoredSsrf:
                    return new AikidoException($"Zen has blocked a stored server-side request forgery during {operation}");

                case AttackKind.OutboundConnectionBlocked:
                    return new AikidoException($"Zen has blocked an outbound connection during {operation}");

                default:
                    return new AikidoException();
            }
        }
    }
}
