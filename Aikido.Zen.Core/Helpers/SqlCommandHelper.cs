using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Vulnerabilities;
using System.Collections.Generic;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// A helper class for SQL commands
    /// </summary>
    public class SqlCommandHelper
    {
        internal static InspectionResult DetectSQLInjection(string commandText, SQLDialect dialect, Context context)
        {
            // check for sql injection against the these inputs
            foreach (var userInput in context.ParsedUserInput)
            {
                var result = SQLInjectionDetector.DetectSQLInjection(commandText, userInput.Value, dialect);

                if (result == SQLInjectionDetectionResult.NotDetected ||
                    (result == SQLInjectionDetectionResult.FailedToTokenize && !EnvironmentHelper.BlockInvalidSql))
                {
                    continue;
                }

                var metadata = new Dictionary<string, object> {
                    { "sql", commandText },
                    { "dialect", dialect.ToHumanName() }
                };

                if (result == SQLInjectionDetectionResult.FailedToTokenize)
                {
                    metadata["failedToTokenize"] = "true";
                }

                return InspectionResult.Attack(
                    AttackKind.SqlInjection,
                    UserInputHelper.GetAttackSourceFromUserInputKey(userInput.Key),
                    userInput.Value,
                    metadata,
                    new[] { UserInputHelper.GetAttackPathFromUserInputKey(userInput.Key) },
                    !EnvironmentHelper.DryMode,
                    AikidoException.SQLInjectionDetected(dialect.ToHumanName())
                );
            }
            return InspectionResult.Continue();
        }
    }
}
