using System;
using System.Collections.Generic;
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
            if (uri == null || context == null)
                return false;

            var hostname = uri.Host;
            var port = uri.Port;

            if (!IPHelper.IsPrivateOrLocalIp(hostname) && !hostname.Equals("localhost", StringComparison.OrdinalIgnoreCase))
                return false;

            if (UrlHelper.IsRequestToItself(context.Url, hostname, port))
                return false;
            foreach (var userInput in context.ParsedUserInput)
            {
                if (ContextHelper.FindHostnameInUserInput(userInput.Value, hostname, port))
                {
                    var metadata = new Dictionary<string, object>
                    {
                        { "hostname", hostname },
                        { "port", port },
                    };
                    var source = userInput.Key.Split('.')[0].ToSource();
                    // send an attack event
                    Agent.Instance.SendAttackEvent(
                        kind: AttackKind.Ssrf,
                        source: HttpHelper.GetSourceFromUserInputPath(userInput.Key),
                        payload: userInput.Value,
                        operation: operation,
                        context: context,
                        module: moduleName,
                        metadata: metadata,
                        blocked: !EnvironmentHelper.DryMode
                    );
                    // set attack detected to true
                    context.AttackDetected = true;
                    return true;
                }
            }
            return false;
        }
    }
}
