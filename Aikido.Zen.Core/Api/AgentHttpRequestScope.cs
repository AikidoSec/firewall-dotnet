using System;
using System.Threading;

namespace Aikido.Zen.Core.Api
{
    internal sealed class AgentHttpRequestScope : IDisposable
    {
        private static readonly AsyncLocal<int> ActiveScopes = new AsyncLocal<int>();
        private bool _disposed;

        private AgentHttpRequestScope()
        {
            ActiveScopes.Value++;
        }

        internal static bool IsActive => ActiveScopes.Value > 0;

        internal static AgentHttpRequestScope Enter()
        {
            return new AgentHttpRequestScope();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ActiveScopes.Value = Math.Max(0, ActiveScopes.Value - 1);
        }
    }
}
