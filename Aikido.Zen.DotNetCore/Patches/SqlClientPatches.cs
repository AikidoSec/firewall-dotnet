using System;
using System.Reflection;
using HarmonyLib;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Helpers;
using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;

namespace Aikido.Zen.DotNetCore.Patches
{
    internal static class SqlClientPatches
    {
        public static void ApplyPatches(Harmony harmony)
        {
            // Use reflection to get the types dynamically
            PatchMethod(harmony, "System.Data.Common", "DbCommand", "ExecuteNonQueryAsync");
            PatchMethod(harmony, "System.Data.Common", "DbCommand", "ExecuteReaderAsync", "System.Data.CommandBehavior");
            PatchMethod(harmony, "System.Data.Common", "DbCommand", "ExecuteScalarAsync");

            // SQL Server
            PatchMethod(harmony, "Microsoft.Data.SqlClient", "SqlCommand", "ExecuteNonQuery");
            PatchMethod(harmony, "Microsoft.Data.SqlClient", "SqlCommand", "ExecuteScalar");
            PatchMethod(harmony, "Microsoft.Data.SqlClient", "SqlCommand", "ExecuteReader", "System.Data.CommandBehavior");
            PatchMethod(harmony, "System.Data.SqlClient", "SqlCommand", "ExecuteNonQuery");
            PatchMethod(harmony, "System.Data.SqlClient", "SqlCommand", "ExecuteScalar");
            PatchMethod(harmony, "System.Data.SqlClient", "SqlCommand", "ExecuteReader", "System.Data.CommandBehavior");

            // SQLite
            PatchMethod(harmony, "Microsoft.Data.Sqlite", "SqliteCommand", "ExecuteNonQuery");
            PatchMethod(harmony, "Microsoft.Data.Sqlite", "SqliteCommand", "ExecuteScalar");
            PatchMethod(harmony, "Microsoft.Data.Sqlite", "SqliteCommand", "ExecuteReader", "System.Data.CommandBehavior");

            // MySql, MariaDB
            PatchMethod(harmony, "MySql.Data", "MySqlClient.MySqlCommand", "ExecuteNonQuery");
            PatchMethod(harmony, "MySql.Data", "MySqlClient.MySqlCommand", "ExecuteScalar");
            PatchMethod(harmony, "MySql.Data", "MySqlClient.MySqlCommand", "ExecuteReader", "System.Data.CommandBehavior");
            PatchMethod(harmony, "MySqlConnector", "MySqlCommand", "ExecuteNonQuery");
            PatchMethod(harmony, "MySqlConnector", "MySqlCommand", "ExecuteScalar");
            PatchMethod(harmony, "MySqlConnector", "MySqlCommand", "ExecuteReader", "System.Data.CommandBehavior");

            // PostgreSQL
            PatchMethod(harmony, "Npgsql", "NpgsqlCommand", "ExecuteNonQuery");
            PatchMethod(harmony, "Npgsql", "NpgsqlCommand", "ExecuteScalar");
            PatchMethod(harmony, "Npgsql", "NpgsqlCommand", "ExecuteReader", "System.Data.CommandBehavior");

            // MySqlX
            PatchMethod(harmony, "MySqlX", "XDevAPI.Relational.Table", "Select");
            PatchMethod(harmony, "MySqlX", "XDevAPI.Relational.Table", "Insert");
            PatchMethod(harmony, "MySqlX", "XDevAPI.Relational.Table", "Update");
            PatchMethod(harmony, "MySqlX", "XDevAPI.Relational.Table", "Delete");

            // NPoco
            PatchMethod(harmony, "NPoco", "Database", "ExecuteReaderHelper", "System.Data.Common.DbCommand");
            PatchMethod(harmony, "NPoco", "Database", "ExecuteNonQueryHelper", "System.Data.Common.DbCommand");
            PatchMethod(harmony, "NPoco", "Database", "ExecuteScalarHelper", "System.Data.Common.DbCommand");

            // EF Core DatabaseFacade
            PatchMethod(harmony, "Microsoft.EntityFrameworkCore.Relational", "RelationalDatabaseFacadeExtensions", "ExecuteSqlRaw", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade", "System.String", "System.Collections.Generic.IEnumerable`1[System.Object]");
            PatchMethod(harmony, "Microsoft.EntityFrameworkCore.Relational", "RelationalDatabaseFacadeExtensions", "ExecuteSqlRawAsync", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade", "System.String", "System.Collections.Generic.IEnumerable`1[System.Object]", "System.Threading.CancellationToken");
        }

        private static void PatchMethod(Harmony harmony, string assemblyName, string typeName, string methodName, params string[] parameterTypeNames)
        {
            var method = ReflectionHelper.GetMethodFromAssembly(assemblyName, typeName, methodName, parameterTypeNames);
            if (method != null)
            {
                harmony.Patch(method, new HarmonyMethod(typeof(SqlClientPatches).GetMethod(nameof(OnCommandExecuting), BindingFlags.Static | BindingFlags.NonPublic)));
            }
        }

        private static bool OnCommandExecuting(object[] __args, MethodBase __originalMethod, object __instance)
        {
            var dbCommand = __instance as System.Data.Common.DbCommand
                ?? __args[0] as System.Data.Common.DbCommand;

            var assembly = __instance?.GetType().Assembly.FullName?.Split(", Culture=")[0] ?? string.Empty;
            string sql = null;

            if (dbCommand != null)
            {
                sql = dbCommand.CommandText;
            }
            else if (__originalMethod.Name.StartsWith("ExecuteSqlRaw"))
            {
                // if the original method is ExecuteSqlRaw or ExecuteSqlRawAsync, we need to get the raw sql from args
                sql = __args[1] as string;
                // because the executeraw methods are extension methods, we don't have an instance to get the assembly from, so we hardcode it here if null
                assembly = "Microsoft.EntityFrameworkCore.Relational";
            }
            else
            {
                // if we can't get the sql, we can't check for SQL injection, so we return true to continue execution
                return true;
            }

            return Aikido.Zen.Core.Patches.SqlClientPatcher.OnCommandExecuting(__args, __originalMethod, sql, assembly, Zen.GetContext());
        }
    }
}
