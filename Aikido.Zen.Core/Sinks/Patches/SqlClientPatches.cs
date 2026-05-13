using System.Data.Common;
using System.Reflection;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core.Sinks
{
    internal static class SqlClientPatches
    {
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
            return SqlClientSink.OnCommandExecuting(
                __instance?.CommandText,
                GetDialect(__instance, __originalMethod),
                __originalMethod,
                Patcher.GetContext());
        }

        [SinkPrefix("NPoco", "NPoco.Database", "ExecuteReaderHelper", "System.Data.Common.DbCommand")]
        [SinkPrefix("NPoco", "NPoco.Database", "ExecuteNonQueryHelper", "System.Data.Common.DbCommand")]
        [SinkPrefix("NPoco", "NPoco.Database", "ExecuteScalarHelper", "System.Data.Common.DbCommand")]
        internal static bool OnCommandExecutingNPocoCommand(DbCommand cmd, MethodBase __originalMethod)
        {
            return SqlClientSink.OnCommandExecuting(
                cmd?.CommandText,
                GetDialect(cmd, __originalMethod),
                __originalMethod,
                Patcher.GetContext());
        }

        [SinkPrefix("Microsoft.EntityFrameworkCore.Relational", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions", "ExecuteSqlRaw", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade", "System.String", "System.Collections.Generic.IEnumerable`1[System.Object]")]
        [SinkPrefix("Microsoft.EntityFrameworkCore.Relational", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions", "ExecuteSqlRawAsync", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade", "System.String", "System.Collections.Generic.IEnumerable`1[System.Object]", "System.Threading.CancellationToken")]
        internal static bool OnCommandExecutingSqlRaw(string sql, MethodBase __originalMethod)
        {
            return SqlClientSink.OnCommandExecuting(
                sql,
                GetDialect(null, __originalMethod),
                __originalMethod,
                Patcher.GetContext());
        }

        [SinkPrefix("MySql.Data", "MySqlX.XDevAPI.Relational.SqlStatement", "Execute")]
        internal static bool OnCommandExecutingMySqlXSqlStatement(object __instance, MethodBase __originalMethod)
        {
            return SqlClientSink.OnCommandExecuting(
                ReflectionHelper.GetStringMember(__instance, "SQL"),
                GetDialect(__instance, __originalMethod),
                __originalMethod,
                Patcher.GetContext());
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
