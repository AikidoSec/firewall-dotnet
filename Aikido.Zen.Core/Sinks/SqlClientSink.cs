using System.Data.Common;
using System.Reflection;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core.Sinks
{
    /// <summary>
    /// Patches for SQL client operations to detect and prevent SQL injection attacks
    /// </summary>
    internal static class SqlClientSink
    {
        internal const string OperationKind = "sql_op";

        [SinkPrefix(typeof(DbCommand), "ExecuteNonQueryAsync")]
        [SinkPrefix(typeof(DbCommand), "ExecuteReaderAsync", "System.Data.CommandBehavior")]
        [SinkPrefix(typeof(DbCommand), "ExecuteScalarAsync")]
        [SinkPrefix("Microsoft.Data.SqlClient", "Microsoft.Data.SqlClient.SqlCommand", "ExecuteNonQuery")]
        [SinkPrefix("Microsoft.Data.SqlClient", "Microsoft.Data.SqlClient.SqlCommand", "ExecuteScalar")]
        [SinkPrefix("Microsoft.Data.SqlClient", "Microsoft.Data.SqlClient.SqlCommand", "ExecuteReader", "System.Data.CommandBehavior")]
        [SinkPrefix("System.Data.SqlClient", "System.Data.SqlClient.SqlCommand", "ExecuteNonQuery")]
        [SinkPrefix("System.Data.SqlClient", "System.Data.SqlClient.SqlCommand", "ExecuteScalar")]
        [SinkPrefix("System.Data.SqlClient", "System.Data.SqlClient.SqlCommand", "ExecuteReader", "System.Data.CommandBehavior")]
        [SinkPrefix("System.Data.SqlServerCe", "System.Data.SqlServerCe.SqlCeCommand", "ExecuteNonQuery")]
        [SinkPrefix("System.Data.SqlServerCe", "System.Data.SqlServerCe.SqlCeCommand", "ExecuteScalar")]
        [SinkPrefix("System.Data.SqlServerCe", "System.Data.SqlServerCe.SqlCeCommand", "ExecuteReader", "System.Data.CommandBehavior")]
        [SinkPrefix("Microsoft.Data.Sqlite", "Microsoft.Data.Sqlite.SqliteCommand", "ExecuteNonQuery")]
        [SinkPrefix("Microsoft.Data.Sqlite", "Microsoft.Data.Sqlite.SqliteCommand", "ExecuteScalar")]
        [SinkPrefix("Microsoft.Data.Sqlite", "Microsoft.Data.Sqlite.SqliteCommand", "ExecuteReader", "System.Data.CommandBehavior")]
        [SinkPrefix("MySql.Data", "MySql.Data.MySqlClient.MySqlCommand", "ExecuteNonQuery")]
        [SinkPrefix("MySql.Data", "MySql.Data.MySqlClient.MySqlCommand", "ExecuteScalar")]
        [SinkPrefix("MySql.Data", "MySql.Data.MySqlClient.MySqlCommand", "ExecuteReader", "System.Data.CommandBehavior")]
        [SinkPrefix("MySqlConnector", "MySqlConnector.MySqlCommand", "ExecuteNonQuery")]
        [SinkPrefix("MySqlConnector", "MySqlConnector.MySqlCommand", "ExecuteScalar")]
        [SinkPrefix("MySqlConnector", "MySqlConnector.MySqlCommand", "ExecuteReader", "System.Data.CommandBehavior")]
        [SinkPrefix("Npgsql", "Npgsql.NpgsqlCommand", "ExecuteNonQuery")]
        [SinkPrefix("Npgsql", "Npgsql.NpgsqlCommand", "ExecuteScalar")]
        [SinkPrefix("Npgsql", "Npgsql.NpgsqlCommand", "ExecuteReader", "System.Data.CommandBehavior")]
        [SinkPrefix("Npgsql", "Npgsql.NpgsqlCommand", "ExecuteNonQueryAsync", "System.Threading.CancellationToken")]
        [SinkPrefix("Npgsql", "Npgsql.NpgsqlCommand", "ExecuteReaderAsync", "System.Threading.CancellationToken")]
        [SinkPrefix("Npgsql", "Npgsql.NpgsqlCommand", "ExecuteReaderAsync", "System.Data.CommandBehavior", "System.Threading.CancellationToken")]
        [SinkPrefix("Npgsql", "Npgsql.NpgsqlCommand", "ExecuteScalarAsync", "System.Threading.CancellationToken")]
        internal static bool OnCommandExecutingDbCommand(DbCommand __instance, MethodBase __originalMethod)
        {
            return SinkAnalyzer.Analyze(
                __originalMethod,
                OperationKind,
                context => OnCommandExecuting(
                    __instance?.CommandText,
                    GetDialect(__instance, __originalMethod),
                    context));
        }

        [SinkPrefix("NPoco", "NPoco.Database", "ExecuteReaderHelper", "System.Data.Common.DbCommand")]
        [SinkPrefix("NPoco", "NPoco.Database", "ExecuteNonQueryHelper", "System.Data.Common.DbCommand")]
        [SinkPrefix("NPoco", "NPoco.Database", "ExecuteScalarHelper", "System.Data.Common.DbCommand")]
        internal static bool OnCommandExecutingNPocoCommand(DbCommand cmd, MethodBase __originalMethod)
        {
            return SinkAnalyzer.Analyze(
                __originalMethod,
                OperationKind,
                context => OnCommandExecuting(
                    cmd?.CommandText,
                    GetDialect(cmd, __originalMethod),
                    context));
        }

        [SinkPrefix("Microsoft.EntityFrameworkCore.Relational", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions", "ExecuteSqlRaw", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade", "System.String", "System.Collections.Generic.IEnumerable`1[System.Object]")]
        [SinkPrefix("Microsoft.EntityFrameworkCore.Relational", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions", "ExecuteSqlRawAsync", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade", "System.String", "System.Collections.Generic.IEnumerable`1[System.Object]", "System.Threading.CancellationToken")]
        internal static bool OnCommandExecutingSqlRaw(string sql, MethodBase __originalMethod)
        {
            return SinkAnalyzer.Analyze(
                __originalMethod,
                OperationKind,
                context => OnCommandExecuting(
                    sql,
                    GetDialect(null, __originalMethod),
                    context));
        }

        [SinkPrefix("MySql.Data", "MySqlX.XDevAPI.Relational.SqlStatement", "Execute")]
        internal static bool OnCommandExecutingMySqlXSqlStatement(object __instance, MethodBase __originalMethod)
        {
            return SinkAnalyzer.Analyze(
                __originalMethod,
                OperationKind,
                context => OnCommandExecuting(
                    ReflectionHelper.GetStringMember(__instance, "SQL"),
                    GetDialect(__instance, __originalMethod),
                    context));
        }

        /// <summary>
        /// Patches the OnCommandExecuting method to detect and prevent SQL injection attacks
        /// </summary>
        /// <param name="sql">The SQL command to execute.</param>
        /// <param name="dialect">The SQL dialect to use for detection.</param>
        /// <param name="context">The current Aikido context.</param>
        internal static InspectionResult OnCommandExecuting(string sql, SQLDialect dialect, Context context)
        {
            var result = InspectionResult.Continue();

            try
            {
                // Perform detection only if context and sql are available
                if (context != null && sql != null)
                {
                    result = SqlCommandHelper.DetectSQLInjection(sql, dialect, context);
                }
            }
            catch
            {
                // Use Agent.Logger (assuming static logger)
                LogHelper.ErrorLog(Agent.Logger, "Error during SQL injection detection.");
                // Allow original method execution despite detection error
                return InspectionResult.Continue();
            }

            return result;
        }

        internal static SQLDialect GetDialect(object instance, MethodBase originalMethod)
        {
            // Prefer the runtime command/provider assembly for base DbCommand patches.
            // Static raw-SQL patches and null instances do not have a useful provider instance,
            // so fall back to the patched method's declaring assembly before defaulting to Generic.
            var assembly = instance?.GetType().Assembly.GetName().Name
                ?? ReflectionHelper.GetMethodModule(originalMethod)
                ?? string.Empty;

            switch (assembly)
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
