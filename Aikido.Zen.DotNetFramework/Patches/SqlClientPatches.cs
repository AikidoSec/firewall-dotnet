using Aikido.Zen.Core.Helpers;
using HarmonyLib;
using Microsoft.Data.SqlClient;
using MySql.Data.MySqlClient;
using System.Data.Common;
using Npgsql;
using MySqlX.XDevAPI.Relational;
using System.Reflection;
using System;

namespace Aikido.Zen.DotNetFramework.Patches
{
    internal static class SqlClientPatches
    {
        public static void ApplyPatches(Harmony harmony)
        {
            // SQL Server
            PatchMethod(harmony, typeof(SqlCommand), "ExecuteNonQuery");
            PatchMethod(harmony, typeof(SqlCommand), "ExecuteScalar");
            PatchMethod(harmony, typeof(SqlCommand), "ExecuteReader", typeof(System.Data.CommandBehavior));

            // SQLite

            // microsoft.data.sqlite
            PatchMethod(harmony, typeof(Microsoft.Data.Sqlite.SqliteCommand), "ExecuteNonQuery");
            PatchMethod(harmony, typeof(Microsoft.Data.Sqlite.SqliteCommand), "ExecuteScalar");
            PatchMethod(harmony, typeof(Microsoft.Data.Sqlite.SqliteCommand), "ExecuteReader", typeof(System.Data.CommandBehavior));
            // system.data.sqlite
            PatchMethod(harmony, typeof(System.Data.SQLite.SQLiteCommand), "ExecuteNonQuery");
            PatchMethod(harmony, typeof(System.Data.SQLite.SQLiteCommand), "ExecuteScalar");
            PatchMethod(harmony, typeof(System.Data.SQLite.SQLiteCommand), "ExecuteReader", typeof(System.Data.CommandBehavior));

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
            var assembly = __instance.GetType().Assembly.FullName?.Split(new[] { ", Culture=" }, StringSplitOptions.None)[0];
            return Aikido.Zen.Core.Patches.SqlClientPatcher.OnCommandExecuting(__args, __originalMethod, __instance, assembly, Zen.GetContext());
        }
    }
}
