using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using HarmonyLib;

namespace Aikido.Zen.Core.Sinks
{
    public class Patcher
    {
        private const string HarmonyId = "aikido.zen";
        private static Func<Context> _getContext = () => null;

        private static readonly MethodInfo IOPathPatch = GetRequiredMethod(typeof(IOSink), nameof(IOSink.OnPathOperation), typeof(object[]), typeof(MethodBase));
        private static readonly MethodInfo IOTwoPathsPatch = GetRequiredMethod(typeof(IOSink), nameof(IOSink.OnTwoPathOperation), typeof(object[]), typeof(MethodBase));
        private static readonly MethodInfo LLMPatch = GetRequiredMethod(typeof(LLMSink), nameof(LLMSink.OnLLMCallCompleted), typeof(object[]), typeof(MethodBase), typeof(object), typeof(object));
        private static readonly MethodInfo OutboundRequestPatch = GetRequiredMethod(typeof(OutboundRequestSink), nameof(OutboundRequestSink.OnRequest), typeof(object[]), typeof(MethodBase), typeof(object));
        private static readonly MethodInfo ProcessExecutionPatch = GetRequiredMethod(typeof(ProcessExecutionSink), nameof(ProcessExecutionSink.OnProcessStart), typeof(object[]), typeof(MethodBase), typeof(object));
        private static readonly MethodInfo SqlClientPatch = GetRequiredMethod(typeof(SqlClientSink), nameof(SqlClientSink.OnCommandExecuting), typeof(object[]), typeof(MethodBase), typeof(object));

        public static void Patch()
        {
            Patch(() => null);
        }

        public static void Patch(Func<Context> getContext)
        {
            try
            {
                Patch(new Harmony(HarmonyId), getContext);
            }
            catch (Exception ex)
            {
                LogHelper.ErrorLog(Agent.Logger, $"Error patching: {ex.Message}");
            }
        }

        public static void Unpatch()
        {
            if (Harmony.HasAnyPatches(HarmonyId))
            {
                var harmony = new Harmony(HarmonyId);
                harmony.UnpatchAll(HarmonyId);
            }
        }

        internal static void Patch(Harmony harmony, Func<Context> getContext)
        {
            _getContext = getContext ?? (() => null);
            PatchSinks(harmony);
        }

        internal static Context GetContext()
        {
            return _getContext();
        }

        private static void PatchSinks(Harmony harmony)
        {
            PatchPrefix(harmony, IOPathPatch, "", "System.IO.File", "Open", "System.String", "System.IO.FileMode");
            PatchPrefix(harmony, IOPathPatch, "", "System.IO.File", "OpenRead", "System.String");
            PatchPrefix(harmony, IOPathPatch, "", "System.IO.File", "OpenWrite", "System.String");
            PatchPrefix(harmony, IOPathPatch, "", "System.IO.File", "Create", "System.String", "System.Int32", "System.IO.FileOptions");
            PatchPrefix(harmony, IOPathPatch, "", "System.IO.File", "Delete", "System.String");
            PatchPrefix(harmony, IOTwoPathsPatch, "", "System.IO.File", "Copy", "System.String", "System.String", "System.Boolean");
            PatchPrefix(harmony, IOTwoPathsPatch, "", "System.IO.File", "Move", "System.String", "System.String");
            PatchPrefix(harmony, IOTwoPathsPatch, "", "System.IO.File", "Move", "System.String", "System.String", "System.Boolean");
            PatchPrefix(harmony, IOPathPatch, "", "System.IO.File", "ReadAllText", "System.String");
            PatchPrefix(harmony, IOPathPatch, "", "System.IO.File", "ReadAllBytes", "System.String");
            PatchPrefix(harmony, IOPathPatch, "", "System.IO.File", "WriteAllText", "System.String", "System.String");
            PatchPrefix(harmony, IOPathPatch, "", "System.IO.File", "WriteAllBytes", "System.String", "System.Byte[]");
            PatchPrefix(harmony, IOPathPatch, "", "System.IO.File", "AppendAllText", "System.String", "System.String");
            PatchPrefix(harmony, IOPathPatch, "", "System.IO.Path", "GetFullPath", "System.String");
            PatchPrefix(harmony, IOTwoPathsPatch, "", "System.IO.Path", "GetFullPath", "System.String", "System.String");
            PatchPrefix(harmony, IOPathPatch, "", "System.IO.Directory", "CreateDirectory", "System.String");
            PatchPrefix(harmony, IOPathPatch, "", "System.IO.Directory", "CreateDirectory", "System.String", "System.Security.AccessControl.DirectorySecurity");
            PatchPrefix(harmony, IOPathPatch, "", "System.IO.Directory", "Delete", "System.String", "System.Boolean");
            PatchPrefix(harmony, IOPathPatch, "", "System.IO.Directory", "GetFiles", "System.String");
            PatchPrefix(harmony, IOPathPatch, "", "System.IO.Directory", "GetFiles", "System.String", "System.String");
            PatchPrefix(harmony, IOPathPatch, "", "System.IO.Directory", "GetFiles", "System.String", "System.String", "System.IO.SearchOption");
            PatchPrefix(harmony, IOPathPatch, "", "System.IO.Directory", "GetDirectories", "System.String");
            PatchPrefix(harmony, IOPathPatch, "", "System.IO.Directory", "GetDirectories", "System.String", "System.String");
            PatchPrefix(harmony, IOPathPatch, "", "System.IO.Directory", "GetDirectories", "System.String", "System.String", "System.IO.SearchOption");

            PatchPostfix(harmony, LLMPatch, "OpenAI", "OpenAI.Chat.ChatClient", "CompleteChat");
            PatchPostfix(harmony, LLMPatch, "OpenAI", "OpenAI.Chat.ChatClient", "CompleteChatAsync");
            PatchPostfix(harmony, LLMPatch, "Rystem.OpenAi", "Rystem.OpenAi.Chat.OpenAiChat", "ExecuteAsync");
            PatchPostfix(harmony, LLMPatch, "Rystem.OpenAi", "Rystem.OpenAi.Chat.OpenAiChat", "ExecuteAsStreamAsync");

            PatchPrefix(harmony, OutboundRequestPatch, "System.Net.Http", "HttpClient", "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Net.Http.HttpCompletionOption", "System.Threading.CancellationToken");
            PatchPrefix(harmony, OutboundRequestPatch, "System.Net.Http", "HttpClient", "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken");
            PatchPrefix(harmony, OutboundRequestPatch, "System.Net.Http", "HttpClient", "Send", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken");
            PatchPrefix(harmony, OutboundRequestPatch, "", "System.Net.WebRequest", "GetResponse");
            PatchPrefix(harmony, OutboundRequestPatch, "", "System.Net.HttpWebRequest", "GetResponse");
            PatchPrefix(harmony, OutboundRequestPatch, "", "System.Net.WebRequest", "GetResponseAsync");

            PatchPrefix(harmony, ProcessExecutionPatch, "System.Diagnostics.Process", "System.Diagnostics.Process", "Start");
            PatchPrefix(harmony, ProcessExecutionPatch, "System", "System.Diagnostics.Process", "Start");

            PatchPrefix(harmony, SqlClientPatch, "System.Data.Common", "DbCommand", "ExecuteNonQueryAsync");
            PatchPrefix(harmony, SqlClientPatch, "System.Data.Common", "DbCommand", "ExecuteReaderAsync", "System.Data.CommandBehavior");
            PatchPrefix(harmony, SqlClientPatch, "System.Data.Common", "DbCommand", "ExecuteScalarAsync");
            PatchPrefix(harmony, SqlClientPatch, "Microsoft.Data.SqlClient", "SqlCommand", "ExecuteNonQuery");
            PatchPrefix(harmony, SqlClientPatch, "Microsoft.Data.SqlClient", "SqlCommand", "ExecuteScalar");
            PatchPrefix(harmony, SqlClientPatch, "Microsoft.Data.SqlClient", "SqlCommand", "ExecuteReader", "System.Data.CommandBehavior");
            PatchPrefix(harmony, SqlClientPatch, "System.Data.SqlClient", "SqlCommand", "ExecuteNonQuery");
            PatchPrefix(harmony, SqlClientPatch, "System.Data.SqlClient", "SqlCommand", "ExecuteScalar");
            PatchPrefix(harmony, SqlClientPatch, "System.Data.SqlClient", "SqlCommand", "ExecuteReader", "System.Data.CommandBehavior");
            PatchPrefix(harmony, SqlClientPatch, "System.Data.SqlServerCe", "SqlCeCommand", "ExecuteNonQuery");
            PatchPrefix(harmony, SqlClientPatch, "System.Data.SqlServerCe", "SqlCeCommand", "ExecuteScalar");
            PatchPrefix(harmony, SqlClientPatch, "System.Data.SqlServerCe", "SqlCeCommand", "ExecuteReader", "System.Data.CommandBehavior");
            PatchPrefix(harmony, SqlClientPatch, "Microsoft.Data.Sqlite", "SqliteCommand", "ExecuteNonQuery");
            PatchPrefix(harmony, SqlClientPatch, "Microsoft.Data.Sqlite", "SqliteCommand", "ExecuteScalar");
            PatchPrefix(harmony, SqlClientPatch, "Microsoft.Data.Sqlite", "SqliteCommand", "ExecuteReader", "System.Data.CommandBehavior");
            PatchPrefix(harmony, SqlClientPatch, "MySql.Data", "MySqlClient.MySqlCommand", "ExecuteNonQuery");
            PatchPrefix(harmony, SqlClientPatch, "MySql.Data", "MySqlClient.MySqlCommand", "ExecuteScalar");
            PatchPrefix(harmony, SqlClientPatch, "MySql.Data", "MySqlClient.MySqlCommand", "ExecuteReader", "System.Data.CommandBehavior");
            PatchPrefix(harmony, SqlClientPatch, "MySqlConnector", "MySqlCommand", "ExecuteNonQuery");
            PatchPrefix(harmony, SqlClientPatch, "MySqlConnector", "MySqlCommand", "ExecuteScalar");
            PatchPrefix(harmony, SqlClientPatch, "MySqlConnector", "MySqlCommand", "ExecuteReader", "System.Data.CommandBehavior");
            PatchPrefix(harmony, SqlClientPatch, "Npgsql", "NpgsqlCommand", "ExecuteNonQuery");
            PatchPrefix(harmony, SqlClientPatch, "Npgsql", "NpgsqlCommand", "ExecuteScalar");
            PatchPrefix(harmony, SqlClientPatch, "Npgsql", "NpgsqlCommand", "ExecuteReader", "System.Data.CommandBehavior");
            PatchPrefix(harmony, SqlClientPatch, "Npgsql", "NpgsqlCommand", "ExecuteNonQueryAsync", "System.Threading.CancellationToken");
            PatchPrefix(harmony, SqlClientPatch, "Npgsql", "NpgsqlCommand", "ExecuteReaderAsync", "System.Threading.CancellationToken");
            PatchPrefix(harmony, SqlClientPatch, "Npgsql", "NpgsqlCommand", "ExecuteReaderAsync", "System.Data.CommandBehavior", "System.Threading.CancellationToken");
            PatchPrefix(harmony, SqlClientPatch, "Npgsql", "NpgsqlCommand", "ExecuteScalarAsync", "System.Threading.CancellationToken");
            PatchPrefix(harmony, SqlClientPatch, "MySqlX", "XDevAPI.Relational.Table", "Select");
            PatchPrefix(harmony, SqlClientPatch, "MySqlX", "XDevAPI.Relational.Table", "Insert");
            PatchPrefix(harmony, SqlClientPatch, "MySqlX", "XDevAPI.Relational.Table", "Update");
            PatchPrefix(harmony, SqlClientPatch, "MySqlX", "XDevAPI.Relational.Table", "Delete");
            PatchPrefix(harmony, SqlClientPatch, "NPoco", "Database", "ExecuteReaderHelper", "System.Data.Common.DbCommand");
            PatchPrefix(harmony, SqlClientPatch, "NPoco", "Database", "ExecuteNonQueryHelper", "System.Data.Common.DbCommand");
            PatchPrefix(harmony, SqlClientPatch, "NPoco", "Database", "ExecuteScalarHelper", "System.Data.Common.DbCommand");
            PatchPrefix(harmony, SqlClientPatch, "Microsoft.EntityFrameworkCore.Relational", "RelationalDatabaseFacadeExtensions", "ExecuteSqlRaw", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade", "System.String", "System.Collections.Generic.IEnumerable`1[System.Object]");
            PatchPrefix(harmony, SqlClientPatch, "Microsoft.EntityFrameworkCore.Relational", "RelationalDatabaseFacadeExtensions", "ExecuteSqlRawAsync", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade", "System.String", "System.Collections.Generic.IEnumerable`1[System.Object]", "System.Threading.CancellationToken");
        }

        private static void PatchPrefix(
            Harmony harmony,
            MethodInfo patchMethod,
            string assemblyName,
            string targetTypeName,
            string targetMethodName,
            params string[] targetParameterTypeNames)
        {
            try
            {
                var assemblyNames = string.IsNullOrEmpty(assemblyName) ? Array.Empty<string>() : new[] { assemblyName };
                var targetMethod = ResolveTargetMethod(assemblyNames, targetTypeName, targetMethodName, targetParameterTypeNames);
                if (targetMethod == null || targetMethod.IsAbstract)
                {
                    return;
                }

                harmony.Patch(targetMethod, prefix: new HarmonyMethod(patchMethod));
            }
            catch (Exception ex)
            {
                LogHelper.ErrorLog(Agent.Logger, $"Error applying patch {targetTypeName}.{targetMethodName}: {ex.Message}");
            }
        }

        private static void PatchPostfix(
            Harmony harmony,
            MethodInfo patchMethod,
            string assemblyName,
            string targetTypeName,
            string targetMethodName,
            params string[] targetParameterTypeNames)
        {
            try
            {
                var assemblyNames = string.IsNullOrEmpty(assemblyName) ? Array.Empty<string>() : new[] { assemblyName };
                var targetMethod = ResolveTargetMethod(assemblyNames, targetTypeName, targetMethodName, targetParameterTypeNames);
                if (targetMethod == null || targetMethod.IsAbstract)
                {
                    return;
                }

                harmony.Patch(targetMethod, postfix: new HarmonyMethod(patchMethod));
            }
            catch (Exception ex)
            {
                LogHelper.ErrorLog(Agent.Logger, $"Error applying patch {targetTypeName}.{targetMethodName}: {ex.Message}");
            }
        }

        private static MethodInfo GetRequiredMethod(Type type, string methodName, params Type[] parameterTypes)
        {
            var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, parameterTypes, null);
            if (method == null)
            {
                throw new MissingMethodException(type.FullName, methodName);
            }

            return method;
        }

        private static MethodInfo ResolveTargetMethod(
            string[] assemblyNames,
            string targetTypeName,
            string targetMethodName,
            string[] targetParameterTypeNames)
        {
            assemblyNames = assemblyNames ?? Array.Empty<string>();
            targetParameterTypeNames = targetParameterTypeNames ?? Array.Empty<string>();

            if (assemblyNames.Length == 0)
            {
                var type = ResolveLoadedType(targetTypeName);
                return type == null ? null : FindTargetMethod(type, targetMethodName, targetParameterTypeNames);
            }

            foreach (var assemblyName in assemblyNames)
            {
                var assembly = LoadAssembly(assemblyName);
                var type = assembly == null ? null : FindTargetType(assembly, targetTypeName);
                var method = type == null ? null : FindTargetMethod(type, targetMethodName, targetParameterTypeNames);
                if (method != null)
                {
                    return method;
                }
            }

            return null;
        }

        private static MethodInfo FindTargetMethod(Type type, string targetMethodName, string[] targetParameterTypeNames)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

            var exactMatch = methods.FirstOrDefault(m => MethodMatches(m, targetMethodName, targetParameterTypeNames));

            if (targetParameterTypeNames.Length > 0)
            {
                return exactMatch;
            }

            return exactMatch ?? methods
                .Where(m => m.Name == targetMethodName)
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
        }

        private static bool MethodMatches(MethodInfo method, string targetMethodName, string[] targetParameterTypeNames)
        {
            if (method.Name != targetMethodName)
            {
                return false;
            }

            var parameters = method.GetParameters();
            if (parameters.Length != targetParameterTypeNames.Length)
            {
                return false;
            }

            return parameters
                .Select((parameter, index) => ParameterTypeMatches(parameter.ParameterType, targetParameterTypeNames[index]))
                .All(matches => matches);
        }

        private static bool ParameterTypeMatches(Type parameterType, string targetParameterTypeName)
        {
            return parameterType.FullName == targetParameterTypeName ||
                parameterType.Name == targetParameterTypeName ||
                parameterType.ToString() == targetParameterTypeName;
        }

        private static Assembly LoadAssembly(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName) || assemblyName.Contains(".."))
            {
                return null;
            }

            var loadedAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == assemblyName);
            if (loadedAssembly != null)
            {
                return loadedAssembly;
            }

            var executingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (!string.IsNullOrEmpty(executingDirectory))
            {
                var assemblyPath = Path.Combine(executingDirectory, $"{assemblyName}.dll");
                if (File.Exists(assemblyPath))
                {
                    return Assembly.LoadFrom(assemblyPath);
                }
            }

            try
            {
                return Assembly.Load(new AssemblyName(assemblyName));
            }
            catch
            {
                return null;
            }
        }

        private static Type ResolveLoadedType(string targetTypeName)
        {
            if (string.IsNullOrEmpty(targetTypeName))
            {
                return null;
            }

            return Type.GetType(targetTypeName) ??
                AppDomain.CurrentDomain
                    .GetAssemblies()
                    .Select(assembly => FindTargetType(assembly, targetTypeName))
                    .FirstOrDefault(type => type != null);
        }

        private static Type FindTargetType(Assembly assembly, string targetTypeName)
        {
            try
            {
                return assembly.ExportedTypes.FirstOrDefault(t => t.FullName == targetTypeName || t.Name == targetTypeName)
                    ?? assembly.GetTypes().FirstOrDefault(t => t.FullName == targetTypeName || t.Name == targetTypeName);
            }
            catch
            {
                return null;
            }
        }
    }
}
