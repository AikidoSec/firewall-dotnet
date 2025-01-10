using Aikido.Zen.Core.Helpers;
using HarmonyLib;
using Microsoft.Data.Sqlite;
using MySql.Data.MySqlClient;
using System.Data.Common;
using Npgsql;
using Aikido.Zen.Core.Models;
using MySqlX.XDevAPI.Relational;
using System.Reflection;

namespace Aikido.Zen.DotNetCore.Patches
{
    internal static class SqlClientPatches
    {
        // we need to patch from inside the framework, because we have to pass the context, which is constructed in a framework specific manner
        public static void ApplyPatches(Harmony harmony)
        {

            // Generic
            PatchMethod(harmony, typeof(DbCommand), "ExecuteNonQueryAsync");
            PatchMethod(harmony, typeof(DbCommand), "ExecuteReaderAsync", typeof(System.Data.CommandBehavior));
            PatchMethod(harmony, typeof(DbCommand), "ExecuteScalarAsync");

            // SQL Server
            PatchMethod(harmony, typeof(Microsoft.Data.SqlClient.SqlCommand), "ExecuteNonQuery");
            PatchMethod(harmony, typeof(Microsoft.Data.SqlClient.SqlCommand), "ExecuteScalar");
            PatchMethod(harmony, typeof(Microsoft.Data.SqlClient.SqlCommand), "ExecuteReader", typeof(System.Data.CommandBehavior));
            PatchMethod(harmony, typeof(System.Data.SqlClient.SqlCommand), "ExecuteNonQuery");
            PatchMethod(harmony, typeof(System.Data.SqlClient.SqlCommand), "ExecuteScalar");
            PatchMethod(harmony, typeof(System.Data.SqlClient.SqlCommand), "ExecuteReader", typeof(System.Data.CommandBehavior));

            // SQLite
            PatchMethod(harmony, typeof(SqliteCommand), "ExecuteNonQuery");
            PatchMethod(harmony, typeof(SqliteCommand), "ExecuteScalar");
            PatchMethod(harmony, typeof(SqliteCommand), "ExecuteReader", typeof(System.Data.CommandBehavior));

            // MySql, MariaDB
            PatchMethod(harmony, typeof(MySqlCommand), "ExecuteNonQuery");
            PatchMethod(harmony, typeof(MySqlCommand), "ExecuteScalar");
            PatchMethod(harmony, typeof(MySqlCommand), "ExecuteReader", typeof(System.Data.CommandBehavior));
            PatchMethod(harmony, typeof(MySqlConnector.MySqlCommand), "ExecuteNonQuery");
            PatchMethod(harmony, typeof(MySqlConnector.MySqlCommand), "ExecuteScalar");
            PatchMethod(harmony, typeof(MySqlConnector.MySqlCommand), "ExecuteReader", typeof(System.Data.CommandBehavior));

            // PostgreSQL
            PatchMethod(harmony, typeof(NpgsqlCommand), "ExecuteNonQuery");
            PatchMethod(harmony, typeof(NpgsqlCommand), "ExecuteScalar");
            PatchMethod(harmony, typeof(NpgsqlCommand), "ExecuteReader", typeof(System.Data.CommandBehavior));

            // MySqlX
            PatchMethod(harmony, typeof(Table), "Select");
            PatchMethod(harmony, typeof(Table), "Insert");
            PatchMethod(harmony, typeof(Table), "Update");
            PatchMethod(harmony, typeof(Table), "Delete");
        }

        private static void PatchMethod(Harmony harmony, Type type, string methodName, params Type[] parameters)
        {
            var method = AccessTools.Method(type, methodName, parameters);
            if (method != null)
            {
                harmony.Patch(method, new HarmonyMethod(typeof(SqlClientPatches).GetMethod(nameof(OnCommandExecuting), BindingFlags.Static | BindingFlags.NonPublic)));
            }
        }

        private static bool OnCommandExecuting(object[] __args, MethodBase __originalMethod, DbCommand __instance)
        {
            var assembly = __instance.GetType().Assembly.FullName?.Split(", Culture=")[0];
            return Aikido.Zen.Core.Patches.SqlClientPatcher.OnCommandExecuting(__args, __originalMethod, __instance, assembly, Zen.GetContext());
        }
    }
}
