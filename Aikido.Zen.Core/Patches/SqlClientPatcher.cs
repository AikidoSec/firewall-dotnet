using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using System.Data.Common;
using System.Reflection;

namespace Aikido.Zen.Core.Patches
{
    /// <summary>
    /// Patches for SQL client operations to detect and prevent SQL injection attacks
    /// </summary>
    public static class SqlClientPatcher
    {
        public static bool OnCommandExecuting(object[] __args, MethodBase __originalMethod, string sql, string assembly, Context context)
        {
            var methodInfo = __originalMethod as MethodInfo;

            if (context == null)
            {
                return true;
            }

            var type = methodInfo?.DeclaringType?.Name ?? "Unknown";
            if (sql != null && SqlCommandHelper.DetectSQLInjection(sql, GetDialect(assembly), context, assembly, $"{type}.{methodInfo?.Name}"))
            {
                // keep going if dry mode
                if (EnvironmentHelper.DryMode)
                {
                    return true;
                }
                throw AikidoException.SQLInjectionDetected(GetDialect(assembly).ToHumanName());
            }
            return true;
        }

        public static SQLDialect GetDialect(string assembly)
        {
            var assemblyName = assembly.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries)?[0]?.Trim();
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
