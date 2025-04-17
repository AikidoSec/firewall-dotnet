using System;
using System.Data.Common;
using System.Diagnostics;
using System.Reflection;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core.Patches
{
    /// <summary>
    /// Patches for SQL client operations to detect and prevent SQL injection attacks
    /// </summary>
    public static class SqlClientPatcher
    {
        private const string kind = "sql_op";
        /// <summary>
        /// Patches the OnCommandExecuting method to detect and prevent SQL injection attacks
        /// </summary>
        /// <param name="__args">The arguments passed to the method.</param>
        /// <param name="__originalMethod">The original method being patched.</param>
        /// <param name="sql">The SQL command to execute.</param>
        public static bool OnCommandExecuting(object[] __args, MethodBase __originalMethod, string sql, string assembly, Context context)
        {

            // Determine sink and context status regardless of detection outcome
            var stopwatch = Stopwatch.StartNew();
            var operation = __originalMethod.DeclaringType.FullName;
            bool withoutContext = context == null;
            bool attackDetected = false;
            bool blocked = false;

            try
            {
                // Perform detection only if context and sql are available
                if (context != null && sql != null)
                {
                    var methodInfo = __originalMethod as MethodInfo;
                    var type = methodInfo?.DeclaringType?.Name ?? "Unknown";
                    var dialect = GetDialect(assembly);

                    attackDetected = SqlCommandHelper.DetectSQLInjection(sql, dialect, context, assembly, $"{type}.{methodInfo?.Name}");
                    blocked = attackDetected && !EnvironmentHelper.DryMode;
                }
            }
            catch
            {
                // Use Agent.Logger (assuming static logger)
                try { LogHelper.DebugLog(Agent.Logger, "Error during SQL injection detection."); } catch {/*ignore*/}
                // Reset flags as detection failed
                attackDetected = false;
                blocked = false;
                // Allow original method execution despite detection error
            }

            // Record the call attempt statistics
            try
            {
                Agent.Instance.Context.OnInspectedCall(operation, kind, stopwatch.Elapsed.TotalMilliseconds, attackDetected, blocked, withoutContext);
            }
            catch
            {
                LogHelper.DebugLog(Agent.Logger, "Error recording OnInspectedCall stats.");
            }

            // Handle blocking if an attack was detected and not in dry mode
            if (blocked)
            {
                // Throwing the exception prevents the original method from running
                throw AikidoException.SQLInjectionDetected(GetDialect(assembly).ToHumanName());
            }

            // Allow the original method to execute
            return true;
        }

        public static SQLDialect GetDialect(string assembly)
        {
            var assemblyName = assembly.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries)?[0]?.Trim();
            switch (assemblyName)
            {
                case "System.Data.SqlClient":
                case "Microsoft.Data.SqlClient":
                case "System.Data.SqlServerCe":
                    return SQLDialect.MicrosoftSQL;
                case "MySql.Data":
                case "MySqlConnector":
                case "MySqlX":
                    return SQLDialect.MySQL;
                case "Npgsql":
                    return SQLDialect.PostgreSQL;
                default:
                    return SQLDialect.Generic;
            }
        }
    }
}
