using System;

namespace Aikido.Zen.Core.Helpers
{
    internal class UriHelper
    {
        internal static (string, int) ExtractHost(Uri requestUri)
        {
            return (requestUri.Host, requestUri.Port);
        }
    }
}
