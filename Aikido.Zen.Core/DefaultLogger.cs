using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Aikido.Zen.Core
{
    internal class DefaultLogger : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var message = state.ToString();
            if (formatter != null)
            {
                message = formatter(state, exception);
            }
            // log to console, by default it works for .Net core web apps
            Console.WriteLine(message);
            // log to debug, by default it works for .Net framework web apps
            Debug.WriteLine(message);
        }
    }
}
