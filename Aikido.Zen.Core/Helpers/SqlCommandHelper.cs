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
                if (SQLInjectionDetector.IsSQLInjection(commandText, userInput.Value, dialect))
                {
                    var metadata = new Dictionary<string, object> {
                        { "sql", commandText },
                        { "userInput", userInput.Value },
                        { "command", commandText }
                    };
                    // send an attack event
                    Agent.Instance.SendAttackEvent(
                        kind: AttackKind.SqlInjection,
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
