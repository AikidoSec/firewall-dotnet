
using System.Collections.Generic;
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
                        { "filename", path }
                    };

                    // Send an attack event
                    Agent.Instance.SendAttackEvent(
                        kind: AttackKind.PathTraversal,
                        source: UserInputHelper.GetAttackSourceFromUserInputKey(userInput.Key), // "query.url" -> "query"
                        payload: userInput.Value,
                        operation: operation,
                        context: context,
                        module: moduleName,
                        metadata: metadata,
                        blocked: !EnvironmentHelper.DryMode,
                        // The `path` argument (filename) is not related to paths here (user input fields eg. query)
                        paths: new[] { UserInputHelper.GetAttackPathFromUserInputKey(userInput.Key) } // "query.url" -> ".url"
                    );

                    // Set attack detected to true
                    context.AttackDetected = true;
                    return true;
                }
            }
            return false;
        }
    }
}
