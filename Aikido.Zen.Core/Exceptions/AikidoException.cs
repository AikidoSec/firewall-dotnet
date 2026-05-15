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

        public static AikidoException Blocked(AttackKind kind, string operation, string metadata = null)
        {
            var metadataSuffix = string.IsNullOrEmpty(metadata) ? string.Empty : $": {metadata}";

            switch (kind)
            {
                case AttackKind.SqlInjection:
                    return new AikidoException($"SQL injection detected during {operation}{metadataSuffix}");

                case AttackKind.ShellInjection:
                    return new AikidoException($"Shell injection detected during {operation}{metadataSuffix}");

                case AttackKind.PathTraversal:
                    return new AikidoException($"Path traversal detected during {operation}{metadataSuffix}");

                case AttackKind.Ssrf:
                    return new AikidoException($"Server-side request forgery detected during {operation}{metadataSuffix}");

                case AttackKind.OutboundConnectionBlocked:
                    return new AikidoException($"Zen has blocked an outbound connection during {operation}{metadataSuffix}");

                default:
                    return new AikidoException();
            }
        }
    }
}
