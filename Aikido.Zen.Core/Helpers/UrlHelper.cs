using System;
using System.Linq;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// Provides operations related to URL validation and manipulation.
    /// </summary>
    public static class UrlHelper
    {
        /// <summary>
        /// Checks if a request is being made to the same server.
        /// </summary>
        /// <param name="serverUrl">The URL of the server.</param>
        /// <param name="outboundHostName">The hostname of the outbound request.</param>
        /// <param name="outboundPort">The port of the outbound request.</param>
        /// <returns>True if the request is to the same server, false otherwise.</returns>
        public static bool IsRequestToItself(string serverUrl, string outboundHostName, int outboundPort)
        {
            // If not trustproxy, return false
            // if (!EnvironmentHelper.TrustProxy)
            //    return false;

            // Try parse server URL
            if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var parsedServerUrl))
                return false;

            // Check hostname match
            if (parsedServerUrl.Host != outboundHostName)
                return false;

            // Check port match
            if (parsedServerUrl.Port != outboundPort)
            {
                // Special production cases
                if ((parsedServerUrl.Port == 80 && outboundPort == 443) ||
                    (parsedServerUrl.Port == 443 && outboundPort == 80))
                    return true;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets the origin URL of a redirect chain.
        /// </summary>
        /// <param name="context">The current request context.</param>
        /// <param name="url">The URL to find the origin for.</param>
        /// <returns>The origin URL of the redirect chain, or null if not found.</returns>
        public static Uri GetRedirectOrigin(Context context, Uri url)
        {
            if (context == null || url == null)
                return null;

            var redirects = context.OutgoingRequestRedirects;
            var currentUrl = url;

            while (true)
            {
                Nullable<Context.RedirectInfo> redirect = redirects.First(r => r.Destination.ToString() == currentUrl.ToString());
                if (redirect == null)
                    break;

                currentUrl = redirect.Value.Source;
            }

            return currentUrl;
        }
    }
}
