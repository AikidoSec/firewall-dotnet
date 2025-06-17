using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Vulnerabilities;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// A helper class for path traversal detection
    /// </summary>
    public class PathTraversalHelper
    {

        public static bool DetectPathTraversal(string path, Context context, string moduleName, string operation)
        {
            // Check for path traversal against the user inputs
            foreach (var userInput in context.ParsedUserInput)
            {
                if (PathTraversalDetector.DetectPathTraversal(userInput.Value, path))
                {
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

                    // Set attack detected to true
                    context.AttackDetected = true;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Validates file operations by checking for path traversal attempts in arguments
        /// </summary>
        /// <param name="args">Array of operation arguments to validate</param>
        /// <param name="method">MethodBase object representing the method being called</param>
        /// <param name="assembly">Assembly name where operation originated</param>
        /// <param name="context">Current execution context</param>
        /// <param name="operation">Name of operation being validated</param>
        /// <returns>True if validation passes, throws exception if path traversal detected in non-dry mode</returns>
        public static bool DetectPathTraversal(object[] args, MethodBase method, string assembly, Context context, string operation)
        {
            // Skip validation if not in request context
            if (context == null)
                return false;

            var arguments = ArgumentHelper.BuildArgumentDictionary(args, method);

            // Validate each argument
            foreach (var arg in arguments)
            {
                // check if the argument name contains path, file or dir
                var paramName = arg.Key.ToLowerInvariant();
                if (!paramName.Contains("path") && !paramName.Contains("file") && !paramName.Contains("dir"))
                    continue;

                switch (arg.Value)
                {
                    case string path:
                        if (DetectPathTraversal(path, context, assembly, operation))
                            return true;
                        break;

                    case string[] paths:
                        if (paths.Any(p => DetectPathTraversal(p, context, assembly, operation)))
                            return true;
                        break;
                }
            }

            return false;
        }
    }
}
