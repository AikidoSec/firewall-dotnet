using System;
using System.Collections.Generic;
using System.Globalization;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// A helper class for SSRF detection and handling
    /// </summary>
    public class SSRFHelper
    {
        /// <summary>
        /// Detects potential SSRF attacks in outbound HTTP requests
        /// </summary>
        /// <param name="uri">The URI being requested</param>
        /// <param name="context">The current request context</param>
        /// <param name="moduleName">The name of the module performing the check</param>
        /// <param name="operation">The operation being performed</param>
        /// <returns>True if an SSRF attack is detected, false otherwise</returns>
        public static bool DetectSSRF(Uri uri, Context context, string moduleName, string operation)
        {
            if (uri == null || context == null || !uri.IsWellFormedOriginalString())
                return false;

            string originalHost = uri.Host;
            string normalizedHostname;

            try
            {
                var idn = new IdnMapping();
                normalizedHostname = idn.GetAscii(originalHost);
                normalizedHostname = normalizedHostname.ToLowerInvariant();
            }
            catch (ArgumentException ex)
            {
                LogHelper.ErrorLog(Agent.Logger, $"Hostname normalization failed for '{originalHost}': {ex.Message}. Using ToLowerInvariant as fallback.");
                normalizedHostname = originalHost.ToLowerInvariant();
            }

            var port = uri.Port;

            if (!IPHelper.IsPrivateOrLocalIp(normalizedHostname) &&
                !normalizedHostname.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (UrlHelper.IsRequestToItself(context.AbsoluteUrl, normalizedHostname, port))
            {
                return false;
            }

            var hostnameLocation = ContextHelper.FindHostnameInContext(context, normalizedHostname, port);
            if (hostnameLocation != null)
            {
                var metadata = new Dictionary<string, object>
                {
                    { "hostname", hostnameLocation.Hostname },
                    { "port", port },
                };
                // send an attack event
                Agent.Instance.SendAttackEvent(
                    kind: AttackKind.Ssrf,
                    source: hostnameLocation.Source,
                    payload: hostnameLocation.Payload,
                    operation: operation,
                    context: context,
                    module: moduleName,
                    metadata: metadata,
                    blocked: !EnvironmentHelper.DryMode
                );

                context.AttackDetected = true;

                return true;
            }
            return false;
        }
    }
}
