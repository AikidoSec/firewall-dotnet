using System;
using System.Data.Common;
using System.Diagnostics;
using System.Reflection;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core.Patches
{
    public static class SqlClientSink
    {
        private const string OperationKind = "sql_op";

        [PatchTarget(PatchKind.Prefix, "System.Data.Common", "DbCommand", "ExecuteNonQueryAsync")]
        [PatchTarget(PatchKind.Prefix, "System.Data.Common", "DbCommand", "ExecuteReaderAsync", "System.Data.CommandBehavior")]
        [PatchTarget(PatchKind.Prefix, "System.Data.Common", "DbCommand", "ExecuteScalarAsync")]
        [PatchTarget(PatchKind.Prefix, "Microsoft.Data.SqlClient", "SqlCommand", "ExecuteNonQuery")]
        [PatchTarget(PatchKind.Prefix, "Microsoft.Data.SqlClient", "SqlCommand", "ExecuteScalar")]
        [PatchTarget(PatchKind.Prefix, "Microsoft.Data.SqlClient", "SqlCommand", "ExecuteReader", "System.Data.CommandBehavior")]
        [PatchTarget(PatchKind.Prefix, "System.Data.SqlClient", "SqlCommand", "ExecuteNonQuery")]
        [PatchTarget(PatchKind.Prefix, "System.Data.SqlClient", "SqlCommand", "ExecuteScalar")]
        [PatchTarget(PatchKind.Prefix, "System.Data.SqlClient", "SqlCommand", "ExecuteReader", "System.Data.CommandBehavior")]
        [PatchTarget(PatchKind.Prefix, "System.Data.SqlServerCe", "SqlCeCommand", "ExecuteNonQuery")]
        [PatchTarget(PatchKind.Prefix, "System.Data.SqlServerCe", "SqlCeCommand", "ExecuteScalar")]
        [PatchTarget(PatchKind.Prefix, "System.Data.SqlServerCe", "SqlCeCommand", "ExecuteReader", "System.Data.CommandBehavior")]
        [PatchTarget(PatchKind.Prefix, "Microsoft.Data.Sqlite", "SqliteCommand", "ExecuteNonQuery")]
        [PatchTarget(PatchKind.Prefix, "Microsoft.Data.Sqlite", "SqliteCommand", "ExecuteScalar")]
        [PatchTarget(PatchKind.Prefix, "Microsoft.Data.Sqlite", "SqliteCommand", "ExecuteReader", "System.Data.CommandBehavior")]
        [PatchTarget(PatchKind.Prefix, "MySql.Data", "MySqlClient.MySqlCommand", "ExecuteNonQuery")]
        [PatchTarget(PatchKind.Prefix, "MySql.Data", "MySqlClient.MySqlCommand", "ExecuteScalar")]
        [PatchTarget(PatchKind.Prefix, "MySql.Data", "MySqlClient.MySqlCommand", "ExecuteReader", "System.Data.CommandBehavior")]
        [PatchTarget(PatchKind.Prefix, "MySqlConnector", "MySqlCommand", "ExecuteNonQuery")]
        [PatchTarget(PatchKind.Prefix, "MySqlConnector", "MySqlCommand", "ExecuteScalar")]
        [PatchTarget(PatchKind.Prefix, "MySqlConnector", "MySqlCommand", "ExecuteReader", "System.Data.CommandBehavior")]
        [PatchTarget(PatchKind.Prefix, "Npgsql", "NpgsqlCommand", "ExecuteNonQuery")]
        [PatchTarget(PatchKind.Prefix, "Npgsql", "NpgsqlCommand", "ExecuteScalar")]
        [PatchTarget(PatchKind.Prefix, "Npgsql", "NpgsqlCommand", "ExecuteReader", "System.Data.CommandBehavior")]
        [PatchTarget(PatchKind.Prefix, "Npgsql", "NpgsqlCommand", "ExecuteNonQueryAsync", "System.Threading.CancellationToken")]
        [PatchTarget(PatchKind.Prefix, "Npgsql", "NpgsqlCommand", "ExecuteReaderAsync", "System.Threading.CancellationToken")]
        [PatchTarget(PatchKind.Prefix, "Npgsql", "NpgsqlCommand", "ExecuteReaderAsync", "System.Data.CommandBehavior", "System.Threading.CancellationToken")]
        [PatchTarget(PatchKind.Prefix, "Npgsql", "NpgsqlCommand", "ExecuteScalarAsync", "System.Threading.CancellationToken")]
        [PatchTarget(PatchKind.Prefix, "MySqlX", "XDevAPI.Relational.Table", "Select")]
        [PatchTarget(PatchKind.Prefix, "MySqlX", "XDevAPI.Relational.Table", "Insert")]
        [PatchTarget(PatchKind.Prefix, "MySqlX", "XDevAPI.Relational.Table", "Update")]
        [PatchTarget(PatchKind.Prefix, "MySqlX", "XDevAPI.Relational.Table", "Delete")]
        [PatchTarget(PatchKind.Prefix, "NPoco", "Database", "ExecuteReaderHelper", "System.Data.Common.DbCommand")]
        [PatchTarget(PatchKind.Prefix, "NPoco", "Database", "ExecuteNonQueryHelper", "System.Data.Common.DbCommand")]
        [PatchTarget(PatchKind.Prefix, "NPoco", "Database", "ExecuteScalarHelper", "System.Data.Common.DbCommand")]
        [PatchTarget(PatchKind.Prefix, "Microsoft.EntityFrameworkCore.Relational", "RelationalDatabaseFacadeExtensions", "ExecuteSqlRaw", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade", "System.String", "System.Collections.Generic.IEnumerable`1[System.Object]")]
        [PatchTarget(PatchKind.Prefix, "Microsoft.EntityFrameworkCore.Relational", "RelationalDatabaseFacadeExtensions", "ExecuteSqlRawAsync", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade", "System.String", "System.Collections.Generic.IEnumerable`1[System.Object]", "System.Threading.CancellationToken")]
        private static bool OnCommandExecuting(object[] __args, MethodBase __originalMethod, object __instance)
        {
            var dbCommand = __instance as DbCommand
                ?? (__args != null && __args.Length > 0 ? __args[0] as DbCommand : null);

            var assembly = __instance?.GetType().Assembly.FullName?.Split(new[] { ", Culture=" }, StringSplitOptions.RemoveEmptyEntries)[0] ?? string.Empty;
            string sql = null;

            if (dbCommand != null)
            {
                sql = dbCommand.CommandText;
            }
            else if (__originalMethod.Name.StartsWith("ExecuteSqlRaw"))
            {
                sql = __args != null && __args.Length > 1 ? __args[1] as string : null;
                assembly = "Microsoft.EntityFrameworkCore.Relational";
            }
            else
            {
                return true;
            }

            return OnCommandExecuting(__args, __originalMethod, sql, assembly, Patcher.GetContext());
        }

        public static bool OnCommandExecuting(object[] __args, MethodBase __originalMethod, string sql, string assembly, Context context)
        {
            if (ReflectionHelper.ShouldSkipAssembly())
            {
                return true;
            }

            if (Context.IsBypassed(context))
            {
                return true;
            }

            var stopwatch = Stopwatch.StartNew();
            var methodInfo = __originalMethod as MethodInfo;
            var operation = $"{methodInfo?.DeclaringType?.Name}.{methodInfo?.Name}";
            var assemblyName = methodInfo?.DeclaringType?.Assembly.GetName().Name;
            var withoutContext = context == null;
            var attackDetected = false;
            var blocked = false;

            try
            {
                if (context != null && sql != null &&
                    !Agent.Instance.Context.IsProtectionDisabledForEndpoint(context))
                {
                    var dialect = GetDialect(assembly ?? assemblyName);

                    attackDetected = SqlCommandHelper.DetectSQLInjection(sql, dialect, context, assemblyName, operation);
                    blocked = attackDetected && !EnvironmentHelper.DryMode;
                }
            }
            catch
            {
                try
                {
                    LogHelper.ErrorLog(Agent.Logger, "Error during SQL injection detection.");
                }
                catch
                {
                }

                attackDetected = false;
                blocked = false;
            }

            try
            {
                Agent.Instance.Context.OnInspectedCall(operation, OperationKind, stopwatch.Elapsed.TotalMilliseconds, attackDetected, blocked, withoutContext);
            }
            catch
            {
                LogHelper.ErrorLog(Agent.Logger, "Error recording OnInspectedCall stats.");
            }

            if (blocked)
            {
                throw AikidoException.SQLInjectionDetected(GetDialect(assembly).ToHumanName());
            }

            return true;
        }

        public static SQLDialect GetDialect(string assembly)
        {
            var assemblyName = assembly.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)?[0]?.Trim();
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
