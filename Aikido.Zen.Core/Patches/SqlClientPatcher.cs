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
        private const string operationKind = "sql_op";
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
            var methodInfo = __originalMethod as MethodInfo;
            var operation = $"{methodInfo?.DeclaringType?.Name}.{methodInfo?.Name}";
            var assemblyName = methodInfo?.DeclaringType?.Assembly.GetName().Name;
            bool withoutContext = context == null;
            bool attackDetected = false;
            bool blocked = false;

            try
            {
                // Perform detection only if context and sql are available
                if (context != null && sql != null)
                {
                    var dialect = GetDialect(assembly ?? assemblyName);

                    attackDetected = SqlCommandHelper.DetectSQLInjection(sql, dialect, context, assemblyName, operation);
                    blocked = attackDetected && !EnvironmentHelper.DryMode;
                }
            }
            catch
            {
                // Use Agent.Logger (assuming static logger)
                try { LogHelper.ErrorLog(Agent.Logger, "Error during SQL injection detection."); } catch {/*ignore*/}
                // Reset flags as detection failed
                attackDetected = false;
                blocked = false;
                // Allow original method execution despite detection error
            }

            // Record the call attempt statistics
            try
            {
                Agent.Instance.Context.OnInspectedCall(operation, operationKind, stopwatch.Elapsed.TotalMilliseconds, attackDetected, blocked, withoutContext);
            }
            catch
            {
                LogHelper.ErrorLog(Agent.Logger, "Error recording OnInspectedCall stats.");
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
