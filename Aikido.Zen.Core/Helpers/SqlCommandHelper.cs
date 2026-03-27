using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Vulnerabilities;
using System;
using System.Collections.Generic;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// A helper class for SQL commands
    /// </summary>
    public class SqlCommandHelper
    {
        public static bool DetectSQLInjection(string commandText, SQLDialect dialect, Context context, string moduleName, string operation)
        {
            // check for sql injection against the these inputs
            foreach (var userInput in context.ParsedUserInput)
            {
                int result = SQLInjectionDetector.DetectSQLInjection(commandText, userInput.Value, dialect);

                if (result == 1)
                {
                    var metadata = new Dictionary<string, object> {
                        { "sql", commandText },
                        { "dialect", dialect.ToHumanName() }
                    };
                    Agent.Instance.SendAttackEvent(
                        kind: AttackKind.SqlInjection,
                        source: UserInputHelper.GetAttackSourceFromUserInputKey(userInput.Key),
                        payload: userInput.Value,
                        operation: operation,
                        context: context,
                        module: moduleName,
                        metadata: metadata,
                        blocked: !EnvironmentHelper.DryMode,
                        paths: new[] { UserInputHelper.GetAttackPathFromUserInputKey(userInput.Key) }
                    );
                    context.AttackDetected = true;
                    return true;
                }

                if (result == 3 && EnvironmentHelper.BlockInvalidSql)
                {
                    var metadata = new Dictionary<string, object> {
                        { "sql", commandText },
                        { "dialect", dialect.ToHumanName() },
                        { "failedToTokenize", "true" }
                    };
                    Agent.Instance.SendAttackEvent(
                        kind: AttackKind.SqlInjection,
                        source: UserInputHelper.GetAttackSourceFromUserInputKey(userInput.Key),
                        payload: userInput.Value,
                        operation: operation,
                        context: context,
                        module: moduleName,
                        metadata: metadata,
                        blocked: !EnvironmentHelper.DryMode,
                        paths: new[] { UserInputHelper.GetAttackPathFromUserInputKey(userInput.Key) }
                    );
                    context.AttackDetected = true;
                    return true;
                }
            }
            return false;
        }
    }
}
