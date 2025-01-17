using System;
using System.Reflection;
using HarmonyLib;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.DotNetFramework.Patches
{
    internal static class SqlClientPatches
    {
        /// <summary>
        /// Applies patches to various database command methods using Harmony.
        /// </summary>
        /// <param name="harmony">The Harmony instance used for patching.</param>
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
        }

        /// <summary>
        /// Patches a method using Harmony by dynamically retrieving it via reflection.
        /// </summary>
        /// <param name="harmony">The Harmony instance used for patching.</param>
        /// <param name="assemblyName">The name of the assembly containing the type.</param>
        /// <param name="typeName">The name of the type containing the method.</param>
        /// <param name="methodName">The name of the method to patch.</param>
        /// <param name="parameterTypeNames">The names of the parameter types for the method.</param>
        private static void PatchMethod(Harmony harmony, string assemblyName, string typeName, string methodName, params string[] parameterTypeNames)
        {
            var method = ReflectionHelper.GetMethodFromAssembly(assemblyName, typeName, methodName, parameterTypeNames);
            if (method != null)
            {
                harmony.Patch(method, new HarmonyMethod(typeof(SqlClientPatches).GetMethod(nameof(OnCommandExecuting), BindingFlags.Static | BindingFlags.NonPublic)));
            }
        }

        /// <summary>
        /// Callback method executed before the original command method is executed.
        /// </summary>
        /// <param name="__args">The arguments passed to the original method.</param>
        /// <param name="__originalMethod">The original method being patched.</param>
        /// <param name="__instance">The instance of the command being executed.</param>
        /// <returns>True if the original method should continue execution; otherwise, false.</returns>
        private static bool OnCommandExecuting(object[] __args, MethodBase __originalMethod, object __instance)
        {
            var dbCommand = __instance as System.Data.Common.DbCommand;
            if (dbCommand == null) return true;
            var assembly = __instance.GetType().Assembly.FullName?.Split(", Culture=")[0];
            return Aikido.Zen.Core.Patches.SqlClientPatcher.OnCommandExecuting(__args, __originalMethod, dbCommand, assembly, Zen.GetContext());
        }
    }
}
