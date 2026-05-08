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
        private static readonly Harmony _harmony = new Harmony(HarmonyId);
        private static Func<Context> _getContext = () => null;

        private static readonly MethodInfo IOPathPatch = GetRequiredMethod(typeof(IOSink), nameof(IOSink.OnPathOperation), typeof(object[]), typeof(MethodBase));
        private static readonly MethodInfo IOTwoPathsPatch = GetRequiredMethod(typeof(IOSink), nameof(IOSink.OnTwoPathOperation), typeof(object[]), typeof(MethodBase));
        private static readonly MethodInfo LLMPatch = GetRequiredMethod(typeof(LLMSink), nameof(LLMSink.OnLLMCallCompleted), typeof(object[]), typeof(MethodBase), typeof(object), typeof(object));
        private static readonly MethodInfo OutboundRequestPatch = GetRequiredMethod(typeof(OutboundRequestSink), nameof(OutboundRequestSink.OnRequest), typeof(object[]), typeof(MethodBase), typeof(object));
        private static readonly MethodInfo ProcessExecutionPatch = GetRequiredMethod(typeof(ProcessExecutionSink), nameof(ProcessExecutionSink.OnProcessStart), typeof(object[]), typeof(MethodBase), typeof(object));
        private static readonly MethodInfo SqlClientPatch = GetRequiredMethod(typeof(SqlClientSink), nameof(SqlClientSink.OnCommandExecuting), typeof(object[]), typeof(MethodBase), typeof(object));

        public static void PatchSinks(Func<Context> getContext)
        {
            try
            {
                _getContext = getContext ?? (() => null);

                PatchPrefix(IOPathPatch, "", "System.IO.File", "Open", "System.String", "System.IO.FileMode");
                PatchPrefix(IOPathPatch, "", "System.IO.File", "OpenRead", "System.String");
                PatchPrefix(IOPathPatch, "", "System.IO.File", "OpenWrite", "System.String");
                PatchPrefix(IOPathPatch, "", "System.IO.File", "Create", "System.String", "System.Int32", "System.IO.FileOptions");
                PatchPrefix(IOPathPatch, "", "System.IO.File", "Delete", "System.String");
                PatchPrefix(IOTwoPathsPatch, "", "System.IO.File", "Copy", "System.String", "System.String", "System.Boolean");
                PatchPrefix(IOTwoPathsPatch, "", "System.IO.File", "Move", "System.String", "System.String");
                PatchPrefix(IOTwoPathsPatch, "", "System.IO.File", "Move", "System.String", "System.String", "System.Boolean");
                PatchPrefix(IOPathPatch, "", "System.IO.File", "ReadAllText", "System.String");
                PatchPrefix(IOPathPatch, "", "System.IO.File", "ReadAllBytes", "System.String");
                PatchPrefix(IOPathPatch, "", "System.IO.File", "WriteAllText", "System.String", "System.String");
                PatchPrefix(IOPathPatch, "", "System.IO.File", "WriteAllBytes", "System.String", "System.Byte[]");
                PatchPrefix(IOPathPatch, "", "System.IO.File", "AppendAllText", "System.String", "System.String");
                PatchPrefix(IOPathPatch, "", "System.IO.Path", "GetFullPath", "System.String");
                PatchPrefix(IOTwoPathsPatch, "", "System.IO.Path", "GetFullPath", "System.String", "System.String");
                PatchPrefix(IOPathPatch, "", "System.IO.Directory", "CreateDirectory", "System.String");
                PatchPrefix(IOPathPatch, "", "System.IO.Directory", "CreateDirectory", "System.String", "System.Security.AccessControl.DirectorySecurity");
                PatchPrefix(IOPathPatch, "", "System.IO.Directory", "Delete", "System.String", "System.Boolean");
                PatchPrefix(IOPathPatch, "", "System.IO.Directory", "GetFiles", "System.String");
                PatchPrefix(IOPathPatch, "", "System.IO.Directory", "GetFiles", "System.String", "System.String");
                PatchPrefix(IOPathPatch, "", "System.IO.Directory", "GetFiles", "System.String", "System.String", "System.IO.SearchOption");
                PatchPrefix(IOPathPatch, "", "System.IO.Directory", "GetDirectories", "System.String");
                PatchPrefix(IOPathPatch, "", "System.IO.Directory", "GetDirectories", "System.String", "System.String");
                PatchPrefix(IOPathPatch, "", "System.IO.Directory", "GetDirectories", "System.String", "System.String", "System.IO.SearchOption");

                PatchPostfix(LLMPatch, "OpenAI", "OpenAI.Chat.ChatClient", "CompleteChat");
                PatchPostfix(LLMPatch, "OpenAI", "OpenAI.Chat.ChatClient", "CompleteChatAsync");
                PatchPostfix(LLMPatch, "Rystem.OpenAi", "Rystem.OpenAi.Chat.OpenAiChat", "ExecuteAsync");
                PatchPostfix(LLMPatch, "Rystem.OpenAi", "Rystem.OpenAi.Chat.OpenAiChat", "ExecuteAsStreamAsync");

                PatchPrefix(OutboundRequestPatch, "System.Net.Http", "HttpClient", "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Net.Http.HttpCompletionOption", "System.Threading.CancellationToken");
                PatchPrefix(OutboundRequestPatch, "System.Net.Http", "HttpClient", "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken");
                PatchPrefix(OutboundRequestPatch, "System.Net.Http", "HttpClient", "Send", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken");
                PatchPrefix(OutboundRequestPatch, "", "System.Net.WebRequest", "GetResponse");
                PatchPrefix(OutboundRequestPatch, "", "System.Net.HttpWebRequest", "GetResponse");
                PatchPrefix(OutboundRequestPatch, "", "System.Net.WebRequest", "GetResponseAsync");

                PatchPrefix(ProcessExecutionPatch, "System.Diagnostics.Process", "System.Diagnostics.Process", "Start");
                PatchPrefix(ProcessExecutionPatch, "System", "System.Diagnostics.Process", "Start");

                PatchPrefix(SqlClientPatch, "System.Data.Common", "DbCommand", "ExecuteNonQueryAsync");
                PatchPrefix(SqlClientPatch, "System.Data.Common", "DbCommand", "ExecuteReaderAsync", "System.Data.CommandBehavior");
                PatchPrefix(SqlClientPatch, "System.Data.Common", "DbCommand", "ExecuteScalarAsync");
                PatchPrefix(SqlClientPatch, "Microsoft.Data.SqlClient", "SqlCommand", "ExecuteNonQuery");
                PatchPrefix(SqlClientPatch, "Microsoft.Data.SqlClient", "SqlCommand", "ExecuteScalar");
                PatchPrefix(SqlClientPatch, "Microsoft.Data.SqlClient", "SqlCommand", "ExecuteReader", "System.Data.CommandBehavior");
                PatchPrefix(SqlClientPatch, "System.Data.SqlClient", "SqlCommand", "ExecuteNonQuery");
                PatchPrefix(SqlClientPatch, "System.Data.SqlClient", "SqlCommand", "ExecuteScalar");
                PatchPrefix(SqlClientPatch, "System.Data.SqlClient", "SqlCommand", "ExecuteReader", "System.Data.CommandBehavior");
                PatchPrefix(SqlClientPatch, "System.Data.SqlServerCe", "SqlCeCommand", "ExecuteNonQuery");
                PatchPrefix(SqlClientPatch, "System.Data.SqlServerCe", "SqlCeCommand", "ExecuteScalar");
                PatchPrefix(SqlClientPatch, "System.Data.SqlServerCe", "SqlCeCommand", "ExecuteReader", "System.Data.CommandBehavior");
                PatchPrefix(SqlClientPatch, "Microsoft.Data.Sqlite", "SqliteCommand", "ExecuteNonQuery");
                PatchPrefix(SqlClientPatch, "Microsoft.Data.Sqlite", "SqliteCommand", "ExecuteScalar");
                PatchPrefix(SqlClientPatch, "Microsoft.Data.Sqlite", "SqliteCommand", "ExecuteReader", "System.Data.CommandBehavior");
                PatchPrefix(SqlClientPatch, "MySql.Data", "MySqlClient.MySqlCommand", "ExecuteNonQuery");
                PatchPrefix(SqlClientPatch, "MySql.Data", "MySqlClient.MySqlCommand", "ExecuteScalar");
                PatchPrefix(SqlClientPatch, "MySql.Data", "MySqlClient.MySqlCommand", "ExecuteReader", "System.Data.CommandBehavior");
                PatchPrefix(SqlClientPatch, "MySqlConnector", "MySqlCommand", "ExecuteNonQuery");
                PatchPrefix(SqlClientPatch, "MySqlConnector", "MySqlCommand", "ExecuteScalar");
                PatchPrefix(SqlClientPatch, "MySqlConnector", "MySqlCommand", "ExecuteReader", "System.Data.CommandBehavior");
                PatchPrefix(SqlClientPatch, "Npgsql", "NpgsqlCommand", "ExecuteNonQuery");
                PatchPrefix(SqlClientPatch, "Npgsql", "NpgsqlCommand", "ExecuteScalar");
                PatchPrefix(SqlClientPatch, "Npgsql", "NpgsqlCommand", "ExecuteReader", "System.Data.CommandBehavior");
                PatchPrefix(SqlClientPatch, "Npgsql", "NpgsqlCommand", "ExecuteNonQueryAsync", "System.Threading.CancellationToken");
                PatchPrefix(SqlClientPatch, "Npgsql", "NpgsqlCommand", "ExecuteReaderAsync", "System.Threading.CancellationToken");
                PatchPrefix(SqlClientPatch, "Npgsql", "NpgsqlCommand", "ExecuteReaderAsync", "System.Data.CommandBehavior", "System.Threading.CancellationToken");
                PatchPrefix(SqlClientPatch, "Npgsql", "NpgsqlCommand", "ExecuteScalarAsync", "System.Threading.CancellationToken");
                PatchPrefix(SqlClientPatch, "MySqlX", "XDevAPI.Relational.Table", "Select");
                PatchPrefix(SqlClientPatch, "MySqlX", "XDevAPI.Relational.Table", "Insert");
                PatchPrefix(SqlClientPatch, "MySqlX", "XDevAPI.Relational.Table", "Update");
                PatchPrefix(SqlClientPatch, "MySqlX", "XDevAPI.Relational.Table", "Delete");
                PatchPrefix(SqlClientPatch, "NPoco", "Database", "ExecuteReaderHelper", "System.Data.Common.DbCommand");
                PatchPrefix(SqlClientPatch, "NPoco", "Database", "ExecuteNonQueryHelper", "System.Data.Common.DbCommand");
                PatchPrefix(SqlClientPatch, "NPoco", "Database", "ExecuteScalarHelper", "System.Data.Common.DbCommand");
                PatchPrefix(SqlClientPatch, "Microsoft.EntityFrameworkCore.Relational", "RelationalDatabaseFacadeExtensions", "ExecuteSqlRaw", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade", "System.String", "System.Collections.Generic.IEnumerable`1[System.Object]");
                PatchPrefix(SqlClientPatch, "Microsoft.EntityFrameworkCore.Relational", "RelationalDatabaseFacadeExtensions", "ExecuteSqlRawAsync", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade", "System.String", "System.Collections.Generic.IEnumerable`1[System.Object]", "System.Threading.CancellationToken");
            }
            catch (Exception ex)
            {
                LogHelper.ErrorLog(Agent.Logger, $"Error patching sinks: {ex.Message}");
            }
        }

        public static void Unpatch()
        {
            if (Harmony.HasAnyPatches(HarmonyId))
            {
                _harmony.UnpatchAll(HarmonyId);
            }
        }

        internal static Context GetContext()
        {
            return _getContext();
        }

        private static void PatchPrefix(
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

                _harmony.Patch(targetMethod, prefix: new HarmonyMethod(patchMethod));
            }
            catch (Exception ex)
            {
                LogHelper.ErrorLog(Agent.Logger, $"Error applying patch {targetTypeName}.{targetMethodName}: {ex.Message}");
            }
        }

        private static void PatchPostfix(
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

                _harmony.Patch(targetMethod, postfix: new HarmonyMethod(patchMethod));
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
