using System.Diagnostics;
using System.Reflection;

using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core.Sinks
{
    /// <summary>
    /// Patches for SQL client operations to detect and prevent SQL injection attacks
    /// </summary>
    internal static class SqlClientSink
    {
        private const string operationKind = "sql_op";

        /// <summary>
        /// Patches the OnCommandExecuting method to detect and prevent SQL injection attacks
        /// </summary>
        /// <param name="sql">The SQL command to execute.</param>
        /// <param name="originalMethod">The original SQL method being inspected.</param>
        /// <param name="context">The current Aikido context.</param>
        internal static bool OnCommandExecuting(string sql, MethodBase originalMethod, Context context)
        {
            // Exclude certain assemblies to avoid stack overflow issues
            if (ReflectionHelper.ShouldSkipAssembly())
            {
                return true;
            }

            if (Context.IsBypassed(context))
            {
                return true;
            }


            var methodInfo = originalMethod as MethodInfo;
            var operation = $"{methodInfo?.DeclaringType?.Name}.{methodInfo?.Name}";
            var module = methodInfo?.DeclaringType?.Assembly.GetName().Name ?? string.Empty;

            // Determine sink and context status regardless of detection outcome
            var stopwatch = Stopwatch.StartNew();
            bool withoutContext = context == null;
            bool attackDetected = false;
            bool blocked = false;
            var dialect = GetDialect(module ?? string.Empty);

            try
            {
                // Perform detection only if context and sql are available
                if (context != null && sql != null &&
                    !Agent.Instance.Context.IsProtectionDisabledForEndpoint(context))
                {
                    attackDetected = SqlCommandHelper.DetectSQLInjection(sql, dialect, context, module, operation);
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
                throw AikidoException.SQLInjectionDetected(dialect.ToHumanName());
            }

            // Allow the original method to execute
            return true;
        }

        internal static SQLDialect GetDialect(string assembly)
        {
            var assemblyName = assembly.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries)[0].Trim();
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
