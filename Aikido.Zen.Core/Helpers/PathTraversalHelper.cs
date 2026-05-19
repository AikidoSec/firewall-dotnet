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
        internal static InspectionResult DetectPathTraversal(string path, Context context)
        {
            if (context == null || context.ParsedUserInput == null)
            {
                return InspectionResult.Allow();
            }

            // Check for path traversal against the user inputs
            foreach (var userInput in context.ParsedUserInput)
            {
                if (PathTraversalDetector.DetectPathTraversal(userInput.Value, path))
                {
                    var metadata = new Dictionary<string, string> {
                        { "filename", path }
                    };

                    return InspectionResult.Block(
                        AttackKind.PathTraversal,
                        UserInputHelper.GetAttackSourceFromUserInputKey(userInput.Key), // "query.url" -> "query"
                        userInput.Value,
                        metadata,
                        new[] { UserInputHelper.GetAttackPathFromUserInputKey(userInput.Key) } // "query.url" -> ".url"
                    );
                }
            }
            return InspectionResult.Allow();
        }
    }
}
