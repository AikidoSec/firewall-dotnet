using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using System.Data.Common;
using System.Reflection;

namespace Aikido.Zen.Core.Patches
{
    public static class SqlClientPatcher
    {
        public static bool OnCommandExecuting(object[] __args, MethodBase __originalMethod, DbCommand __instance, string assembly, Context context)
        {
            var command = __instance;
            var methodInfo = __originalMethod as MethodInfo;

            if (context == null)
            {
                return true;
            }
            var type = __instance.GetType().Name;
            if (command != null && SqlCommandHelper.DetectSQLInjection(command.CommandText, GetDialect(assembly), context, assembly, $"{type}.{methodInfo.Name}"))
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
                    return SQLDialect.MicrosoftSQL;
                case "Microsoft.Data.Sqlite":
                    return SQLDialect.Generic;
                case "MySql.Data":
                    return SQLDialect.MySQL;
                case "Npgsql":
                    return SQLDialect.PostgreSQL;
                case "MySqlConnector":
                    return SQLDialect.MySQL;
                default:
                    return SQLDialect.Generic;
            }
        }
    }
}
