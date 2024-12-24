using Aikido.Zen.Core.Helpers;
using HarmonyLib;
using Microsoft.Data.SqlClient;
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
            // SQL Server
            PatchMethod(harmony, typeof(SqlCommand), "ExecuteNonQuery");
            PatchMethod(harmony, typeof(SqlCommand), "ExecuteScalar");
            PatchMethod(harmony, typeof(SqlCommand), "ExecuteReader", typeof(System.Data.CommandBehavior));

            // SQLite
            PatchMethod(harmony, typeof(SqliteCommand), "ExecuteNonQuery");
            PatchMethod(harmony, typeof(SqliteCommand), "ExecuteScalar");
            PatchMethod(harmony, typeof(SqliteCommand), "ExecuteReader", typeof(System.Data.CommandBehavior));

            // MySql, MariaDB
            PatchMethod(harmony, typeof(MySqlCommand), "ExecuteNonQuery");
            PatchMethod(harmony, typeof(MySqlCommand), "ExecuteScalar");
            PatchMethod(harmony, typeof(MySqlCommand), "ExecuteReader", typeof(System.Data.CommandBehavior));

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
