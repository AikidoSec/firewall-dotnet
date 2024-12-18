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
using Aikido.Zen.Core.Exceptions;
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
            var command = __instance;
            var methodInfo = __originalMethod as MethodInfo;
            var context = Zen.GetContext();

            if (context == null)
            {
                return true;
            }
            if (command != null && SqlCommandHelper.DetectSQLInjection(command.CommandText, GetDialect(command, out var type, out var assembly), context, assembly, $"{type}.{methodInfo.Name}"))
            {
                // keep going if dry mode
                if (EnvironmentHelper.DryMode)
                {
                    return true;
                }
                throw AikidoException.SQLInjectionDetected(command.CommandText);
            }
            return true;
        }

        private static SQLDialect GetDialect(DbCommand dbCommand, out string type, out string assembly)
        {
            if (dbCommand is SqlCommand)
            {
                type = nameof(SqlCommand);
                assembly = typeof(SqlCommand).Assembly.FullName?.Split(new string[] { ", Culture=" }, StringSplitOptions.None)[0];
                return SQLDialect.MicrosoftSQL;
            }
            else if (dbCommand is SqliteCommand)
            {
                type = nameof(SqliteCommand);
                assembly = typeof(SqliteCommand).Assembly.FullName.Split(new string[] { ", Culture=" }, StringSplitOptions.None)[0];
                return SQLDialect.Generic;
            }
            else if (dbCommand is MySqlCommand)
            {
                type = nameof(MySqlCommand);
                assembly = typeof(MySqlCommand).AssemblyQualifiedName;
                return SQLDialect.MySQL;
            }
            else if (dbCommand is NpgsqlCommand)
            {
                type = nameof(NpgsqlCommand);
                assembly = typeof(NpgsqlCommand).AssemblyQualifiedName;
                return SQLDialect.PostgreSQL;
            }
            type = null;
            assembly = null;
            return SQLDialect.Generic;
        }
    }
}
