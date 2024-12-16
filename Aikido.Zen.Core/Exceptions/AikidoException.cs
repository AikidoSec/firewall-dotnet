using System;

namespace Aikido.Zen.Core.Exceptions
{
    public class AikidoException : Exception
    {

        private const string _defaultMessage = "Unknown threat blocked";
        public AikidoException(string message = _defaultMessage) : base(!string.IsNullOrEmpty(message) ? message : _defaultMessage)
        {

        }

        public static AikidoException SQLInjectionDetected(string query)
        {
            return new AikidoException($"SQL injection detected in query: {query}");
        }

        public static AikidoException ShellInjectionDetected(string command)
        {
            return new AikidoException($"Shell injection detected in command: {command}");
        }

        public static AikidoException RequestBlocked(string route, string ipAddress)
        {
            return new AikidoException($"Request blocked from {ipAddress} to {route}");
        }
    }
}
