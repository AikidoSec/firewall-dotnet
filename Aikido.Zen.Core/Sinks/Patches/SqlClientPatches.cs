using System.Data.Common;
using System.Reflection;
using Aikido.Zen.Core.Helpers;

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
            return SqlClientSink.OnCommandExecuting(__instance?.CommandText, __originalMethod, Patcher.GetContext());
        }

        [SinkPrefix("NPoco", "NPoco.Database", "ExecuteReaderHelper", "System.Data.Common.DbCommand")]
        [SinkPrefix("NPoco", "NPoco.Database", "ExecuteNonQueryHelper", "System.Data.Common.DbCommand")]
        [SinkPrefix("NPoco", "NPoco.Database", "ExecuteScalarHelper", "System.Data.Common.DbCommand")]
        internal static bool OnCommandExecutingNPocoCommand(DbCommand cmd, MethodBase __originalMethod)
        {
            return SqlClientSink.OnCommandExecuting(cmd?.CommandText, __originalMethod, Patcher.GetContext());
        }

        [SinkPrefix("Microsoft.EntityFrameworkCore.Relational", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions", "ExecuteSqlRaw", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade", "System.String", "System.Collections.Generic.IEnumerable`1[System.Object]")]
        [SinkPrefix("Microsoft.EntityFrameworkCore.Relational", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions", "ExecuteSqlRawAsync", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade", "System.String", "System.Collections.Generic.IEnumerable`1[System.Object]", "System.Threading.CancellationToken")]
        internal static bool OnCommandExecutingSqlRaw(string sql, MethodBase __originalMethod)
        {
            return SqlClientSink.OnCommandExecuting(sql, __originalMethod, Patcher.GetContext());
        }

        [SinkPrefix("MySql.Data", "MySqlX.XDevAPI.Relational.SqlStatement", "Execute")]
        internal static bool OnCommandExecutingMySqlXSqlStatement(object __instance, MethodBase __originalMethod)
        {
            return SqlClientSink.OnCommandExecuting(ReflectionHelper.GetStringMember(__instance, "SQL"), __originalMethod, Patcher.GetContext());
        }
    }
}
