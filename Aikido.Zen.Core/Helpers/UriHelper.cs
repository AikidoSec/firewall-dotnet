using System;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// Helper class for URI related operations.
    /// </summary>
    internal class UriHelper
    {
        /// <summary>
        /// Extracts the host and port from a URI.
        /// </summary>
        /// <param name="requestUri">The URI to extract host information from.</param>
        /// <returns>A tuple containing the host name (string) and port number (int).</returns>
        internal static (string, int) ExtractHost(Uri requestUri)
        {
            return (requestUri.Host, requestUri.Port);
        }
    }
}
