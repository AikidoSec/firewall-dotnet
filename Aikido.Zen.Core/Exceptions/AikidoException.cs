using System;

namespace Aikido.Zen.Core.Exceptions
{
    public class AikidoException : Exception
    {

        private const string _defaultMessage = "Unknown threat blocked";
        public AikidoException(string message = _defaultMessage) : base(!string.IsNullOrEmpty(message) ? message : _defaultMessage)
        {

        }

        public static AikidoException SQLInjectionDetected(string dialect)
        {
            return new AikidoException($"{dialect}: SQL injection detected");
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
