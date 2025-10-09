using System.Collections.Generic;
using System.Linq;

using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Vulnerabilities;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// A helper class for path traversal detection
    /// </summary>
    public class PathTraversalHelper
    {

        /// <summary>
        /// Validates file operations by checking for path traversal attempts in arguments
        /// </summary>
        /// <param name="paths">Array of paths to validate</param>
        /// <param name="assembly">Assembly name where operation originated</param>
        /// <param name="context">Current execution context</param>
        /// <param name="operation">Name of operation being validated</param>
        /// <returns>True if validation passes, throws exception if path traversal detected in non-dry mode</returns>
        public static bool DetectPathTraversal(string[] paths, string assembly, Context context, string operation)
        {
            // Skip validation if not in request context
            if (context == null)
                return false;

            // Validate each argument
            foreach (var path in paths)            
                if (CheckPath(path, context, assembly, operation))
                    return true;           

            return false;
        }

        /// <summary>
        /// Checks a given path against user inputs for path traversal attempts
        /// </summary>
        /// <returns></returns>
        private static bool CheckPath(string path, Context context, string moduleName, string operation)
        {
            // Check for path traversal against the user inputs
            foreach (var userInput in context.ParsedUserInput)
            {
                if (!PathTraversalDetector.DetectPathTraversal(userInput.Value, path))
                    continue;

                var metadata = new Dictionary<string, object> {
                        { "path", path }
                    };

                // Send an attack event
                Agent.Instance.SendAttackEvent(
                    kind: AttackKind.PathTraversal,
                    source: HttpHelper.GetSourceFromUserInputPath(userInput.Key),
                    payload: userInput.Value,
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
