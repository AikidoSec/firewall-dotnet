
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

namespace Aikido.Zen.DotNetCore.Patches
{
    // SQL server
    [HarmonyPatch(typeof(SqlCommand), "ExecuteNonQuery")]
    [HarmonyPatch(typeof(SqlCommand), "ExecuteScalar")]
    [HarmonyPatch(typeof(SqlCommand), "ExecuteReader")]
    // SQLite
    [HarmonyPatch(typeof(SqliteCommand), "ExecuteNonQuery")]
    [HarmonyPatch(typeof(SqliteCommand), "ExecuteScalar")]
    [HarmonyPatch(typeof(SqliteCommand), "ExecuteReader")]
    // MySql, MariaDB
    [HarmonyPatch(typeof(MySqlCommand), "ExecuteNonQuery")]
    [HarmonyPatch(typeof(MySqlCommand), "ExecuteScalar")]
    [HarmonyPatch(typeof(MySqlCommand), "ExecuteReader")]
    // PostgreSQL
    [HarmonyPatch(typeof(NpgsqlCommand), "ExecuteNonQuery")]
    [HarmonyPatch(typeof(NpgsqlCommand), "ExecuteScalar")]
    [HarmonyPatch(typeof(NpgsqlCommand), "ExecuteReader")]

    internal class SqlClientPatches
    {
        [HarmonyPrefix]
        internal static bool OnCommandExecuting(MethodInfo methodInfo, DbCommand command)
        {
        var context = Zen.GetContext();
            if (context == null) {
                return true;
            }
            if (SqlCommandHelper.DetectSQLInjection(command.CommandText, GetDialect(command, out var type, out var assembly), context, assembly, $"{type}.{methodInfo.Name}")) {
                // keep going if dry mode
                if (EnvironmentHelper.DryMode)
                {
                    return true;
                }
                throw AikidoException.SQLInjectionDetected(command.CommandText);
            }
            return true;
        }

        private static SQLDialect GetDialect(DbCommand dbCommand, out string type, out string assembly) {
            // get the dialect of the command
            if (dbCommand is SqlCommand) {
                type = nameof(SqlCommand);
                assembly = typeof(SqlCommand).AssemblyQualifiedName;
                return SQLDialect.MicrosoftSQL;
            } else if (dbCommand is SqliteCommand) {
                type = nameof(SqliteCommand);
                assembly = typeof(SqliteCommand).AssemblyQualifiedName;
                return SQLDialect.Generic;
            } else if (dbCommand is MySqlCommand) {
                type = nameof(MySqlCommand);
                assembly = typeof(MySqlCommand).AssemblyQualifiedName;
                return SQLDialect.MySQL;
            } else if (dbCommand is NpgsqlCommand) {
                type = nameof(NpgsqlCommand);
                assembly = typeof(NpgsqlCommand).AssemblyQualifiedName;
                return SQLDialect.PostgreSQL;
            }
            type = null;
            assembly = null;
            return SQLDialect.Generic;
        }
    }

    [HarmonyPatch(typeof(Table), "Select")]
    [HarmonyPatch(typeof(Table), "Insert")]
    [HarmonyPatch(typeof(Table), "Update")]
    [HarmonyPatch(typeof(Table), "Delete")]
    internal class MySqlXPatches
    {
        internal static bool OnCommandExecuting(MethodInfo methodInfo, SqlStatement command)
        {
            var context = Zen.GetContext();
            if (context == null)
            {
                return true;
            }
            if (SqlCommandHelper.DetectSQLInjection(command.SQL, SQLDialect.MySQL, context, typeof(Table).AssemblyQualifiedName, typeof(Table).Name + "." + methodInfo.Name))
            {
                // keep going if dry mode
                if (EnvironmentHelper.DryMode)
                {
                    return true;
                }
                throw AikidoException.SQLInjectionDetected(command.SQL);
            }
            return true;
        }
    }
}
