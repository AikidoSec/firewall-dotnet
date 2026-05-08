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
            PatchDefinitions(harmony);
        }

        internal static Context GetContext()
        {
            return _getContext();
        }

        private static void PatchDefinitions(Harmony harmony)
        {
            Patch(harmony, PatchDefinition.Prefix(IOPathPatch, "", "System.IO.File", "Open", "System.String", "System.IO.FileMode"));
            Patch(harmony, PatchDefinition.Prefix(IOPathPatch, "", "System.IO.File", "OpenRead", "System.String"));
            Patch(harmony, PatchDefinition.Prefix(IOPathPatch, "", "System.IO.File", "OpenWrite", "System.String"));
            Patch(harmony, PatchDefinition.Prefix(IOPathPatch, "", "System.IO.File", "Create", "System.String", "System.Int32", "System.IO.FileOptions"));
            Patch(harmony, PatchDefinition.Prefix(IOPathPatch, "", "System.IO.File", "Delete", "System.String"));
            Patch(harmony, PatchDefinition.Prefix(IOTwoPathsPatch, "", "System.IO.File", "Copy", "System.String", "System.String", "System.Boolean"));
            Patch(harmony, PatchDefinition.Prefix(IOTwoPathsPatch, "", "System.IO.File", "Move", "System.String", "System.String"));
            Patch(harmony, PatchDefinition.Prefix(IOTwoPathsPatch, "", "System.IO.File", "Move", "System.String", "System.String", "System.Boolean"));
            Patch(harmony, PatchDefinition.Prefix(IOPathPatch, "", "System.IO.File", "ReadAllText", "System.String"));
            Patch(harmony, PatchDefinition.Prefix(IOPathPatch, "", "System.IO.File", "ReadAllBytes", "System.String"));
            Patch(harmony, PatchDefinition.Prefix(IOPathPatch, "", "System.IO.File", "WriteAllText", "System.String", "System.String"));
            Patch(harmony, PatchDefinition.Prefix(IOPathPatch, "", "System.IO.File", "WriteAllBytes", "System.String", "System.Byte[]"));
            Patch(harmony, PatchDefinition.Prefix(IOPathPatch, "", "System.IO.File", "AppendAllText", "System.String", "System.String"));
            Patch(harmony, PatchDefinition.Prefix(IOPathPatch, "", "System.IO.Path", "GetFullPath", "System.String"));
            Patch(harmony, PatchDefinition.Prefix(IOTwoPathsPatch, "", "System.IO.Path", "GetFullPath", "System.String", "System.String"));
            Patch(harmony, PatchDefinition.Prefix(IOPathPatch, "", "System.IO.Directory", "CreateDirectory", "System.String"));
            Patch(harmony, PatchDefinition.Prefix(IOPathPatch, "", "System.IO.Directory", "CreateDirectory", "System.String", "System.Security.AccessControl.DirectorySecurity"));
            Patch(harmony, PatchDefinition.Prefix(IOPathPatch, "", "System.IO.Directory", "Delete", "System.String", "System.Boolean"));
            Patch(harmony, PatchDefinition.Prefix(IOPathPatch, "", "System.IO.Directory", "GetFiles", "System.String"));
            Patch(harmony, PatchDefinition.Prefix(IOPathPatch, "", "System.IO.Directory", "GetFiles", "System.String", "System.String"));
            Patch(harmony, PatchDefinition.Prefix(IOPathPatch, "", "System.IO.Directory", "GetFiles", "System.String", "System.String", "System.IO.SearchOption"));
            Patch(harmony, PatchDefinition.Prefix(IOPathPatch, "", "System.IO.Directory", "GetDirectories", "System.String"));
            Patch(harmony, PatchDefinition.Prefix(IOPathPatch, "", "System.IO.Directory", "GetDirectories", "System.String", "System.String"));
            Patch(harmony, PatchDefinition.Prefix(IOPathPatch, "", "System.IO.Directory", "GetDirectories", "System.String", "System.String", "System.IO.SearchOption"));

            Patch(harmony, PatchDefinition.Postfix(LLMPatch, "OpenAI", "OpenAI.Chat.ChatClient", "CompleteChat"));
            Patch(harmony, PatchDefinition.Postfix(LLMPatch, "OpenAI", "OpenAI.Chat.ChatClient", "CompleteChatAsync"));
            Patch(harmony, PatchDefinition.Postfix(LLMPatch, "Rystem.OpenAi", "Rystem.OpenAi.Chat.OpenAiChat", "ExecuteAsync"));
            Patch(harmony, PatchDefinition.Postfix(LLMPatch, "Rystem.OpenAi", "Rystem.OpenAi.Chat.OpenAiChat", "ExecuteAsStreamAsync"));

            Patch(harmony, PatchDefinition.Prefix(OutboundRequestPatch, "System.Net.Http", "HttpClient", "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Net.Http.HttpCompletionOption", "System.Threading.CancellationToken"));
            Patch(harmony, PatchDefinition.Prefix(OutboundRequestPatch, "System.Net.Http", "HttpClient", "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken"));
            Patch(harmony, PatchDefinition.Prefix(OutboundRequestPatch, "System.Net.Http", "HttpClient", "Send", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken"));
            Patch(harmony, PatchDefinition.Prefix(OutboundRequestPatch, "", "System.Net.WebRequest", "GetResponse"));
            Patch(harmony, PatchDefinition.Prefix(OutboundRequestPatch, "", "System.Net.HttpWebRequest", "GetResponse"));
            Patch(harmony, PatchDefinition.Prefix(OutboundRequestPatch, "", "System.Net.WebRequest", "GetResponseAsync"));

            Patch(harmony, PatchDefinition.Prefix(ProcessExecutionPatch, new[] { "System.Diagnostics.Process", "System" }, "System.Diagnostics.Process", "Start"));

            Patch(harmony, PatchDefinition.Prefix(SqlClientPatch, "System.Data.Common", "DbCommand", "ExecuteNonQueryAsync"));
            Patch(harmony, PatchDefinition.Prefix(SqlClientPatch, "System.Data.Common", "DbCommand", "ExecuteReaderAsync", "System.Data.CommandBehavior"));
            Patch(harmony, PatchDefinition.Prefix(SqlClientPatch, "System.Data.Common", "DbCommand", "ExecuteScalarAsync"));
            Patch(harmony, PatchDefinition.Prefix(SqlClientPatch, "Microsoft.Data.SqlClient", "SqlCommand", "ExecuteNonQuery"));
            Patch(harmony, PatchDefinition.Prefix(SqlClientPatch, "Microsoft.Data.SqlClient", "SqlCommand", "ExecuteScalar"));
            Patch(harmony, PatchDefinition.Prefix(SqlClientPatch, "Microsoft.Data.SqlClient", "SqlCommand", "ExecuteReader", "System.Data.CommandBehavior"));
            Patch(harmony, PatchDefinition.Prefix(SqlClientPatch, "System.Data.SqlClient", "SqlCommand", "ExecuteNonQuery"));
            Patch(harmony, PatchDefinition.Prefix(SqlClientPatch, "System.Data.SqlClient", "SqlCommand", "ExecuteScalar"));
            Patch(harmony, PatchDefinition.Prefix(SqlClientPatch, "System.Data.SqlClient", "SqlCommand", "ExecuteReader", "System.Data.CommandBehavior"));
            Patch(harmony, PatchDefinition.Prefix(SqlClientPatch, "System.Data.SqlServerCe", "SqlCeCommand", "ExecuteNonQuery"));
            Patch(harmony, PatchDefinition.Prefix(SqlClientPatch, "System.Data.SqlServerCe", "SqlCeCommand", "ExecuteScalar"));
            Patch(harmony, PatchDefinition.Prefix(SqlClientPatch, "System.Data.SqlServerCe", "SqlCeCommand", "ExecuteReader", "System.Data.CommandBehavior"));
            Patch(harmony, PatchDefinition.Prefix(SqlClientPatch, "Microsoft.Data.Sqlite", "SqliteCommand", "ExecuteNonQuery"));
            Patch(harmony, PatchDefinition.Prefix(SqlClientPatch, "Microsoft.Data.Sqlite", "SqliteCommand", "ExecuteScalar"));
            Patch(harmony, PatchDefinition.Prefix(SqlClientPatch, "Microsoft.Data.Sqlite", "SqliteCommand", "ExecuteReader", "System.Data.CommandBehavior"));
            Patch(harmony, PatchDefinition.Prefix(SqlClientPatch, "MySql.Data", "MySqlClient.MySqlCommand", "ExecuteNonQuery"));
            Patch(harmony, PatchDefinition.Prefix(SqlClientPatch, "MySql.Data", "MySqlClient.MySqlCommand", "ExecuteScalar"));
            Patch(harmony, PatchDefinition.Prefix(SqlClientPatch, "MySql.Data", "MySqlClient.MySqlCommand", "ExecuteReader", "System.Data.CommandBehavior"));
            Patch(harmony, PatchDefinition.Prefix(SqlClientPatch, "MySqlConnector", "MySqlCommand", "ExecuteNonQuery"));
            Patch(harmony, PatchDefinition.Prefix(SqlClientPatch, "MySqlConnector", "MySqlCommand", "ExecuteScalar"));
            Patch(harmony, PatchDefinition.Prefix(SqlClientPatch, "MySqlConnector", "MySqlCommand", "ExecuteReader", "System.Data.CommandBehavior"));
            Patch(harmony, PatchDefinition.Prefix(SqlClientPatch, "Npgsql", "NpgsqlCommand", "ExecuteNonQuery"));
            Patch(harmony, PatchDefinition.Prefix(SqlClientPatch, "Npgsql", "NpgsqlCommand", "ExecuteScalar"));
            Patch(harmony, PatchDefinition.Prefix(SqlClientPatch, "Npgsql", "NpgsqlCommand", "ExecuteReader", "System.Data.CommandBehavior"));
            Patch(harmony, PatchDefinition.Prefix(SqlClientPatch, "Npgsql", "NpgsqlCommand", "ExecuteNonQueryAsync", "System.Threading.CancellationToken"));
            Patch(harmony, PatchDefinition.Prefix(SqlClientPatch, "Npgsql", "NpgsqlCommand", "ExecuteReaderAsync", "System.Threading.CancellationToken"));
            Patch(harmony, PatchDefinition.Prefix(SqlClientPatch, "Npgsql", "NpgsqlCommand", "ExecuteReaderAsync", "System.Data.CommandBehavior", "System.Threading.CancellationToken"));
            Patch(harmony, PatchDefinition.Prefix(SqlClientPatch, "Npgsql", "NpgsqlCommand", "ExecuteScalarAsync", "System.Threading.CancellationToken"));
            Patch(harmony, PatchDefinition.Prefix(SqlClientPatch, "MySqlX", "XDevAPI.Relational.Table", "Select"));
            Patch(harmony, PatchDefinition.Prefix(SqlClientPatch, "MySqlX", "XDevAPI.Relational.Table", "Insert"));
            Patch(harmony, PatchDefinition.Prefix(SqlClientPatch, "MySqlX", "XDevAPI.Relational.Table", "Update"));
            Patch(harmony, PatchDefinition.Prefix(SqlClientPatch, "MySqlX", "XDevAPI.Relational.Table", "Delete"));
            Patch(harmony, PatchDefinition.Prefix(SqlClientPatch, "NPoco", "Database", "ExecuteReaderHelper", "System.Data.Common.DbCommand"));
            Patch(harmony, PatchDefinition.Prefix(SqlClientPatch, "NPoco", "Database", "ExecuteNonQueryHelper", "System.Data.Common.DbCommand"));
            Patch(harmony, PatchDefinition.Prefix(SqlClientPatch, "NPoco", "Database", "ExecuteScalarHelper", "System.Data.Common.DbCommand"));
            Patch(harmony, PatchDefinition.Prefix(SqlClientPatch, "Microsoft.EntityFrameworkCore.Relational", "RelationalDatabaseFacadeExtensions", "ExecuteSqlRaw", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade", "System.String", "System.Collections.Generic.IEnumerable`1[System.Object]"));
            Patch(harmony, PatchDefinition.Prefix(SqlClientPatch, "Microsoft.EntityFrameworkCore.Relational", "RelationalDatabaseFacadeExtensions", "ExecuteSqlRawAsync", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade", "System.String", "System.Collections.Generic.IEnumerable`1[System.Object]", "System.Threading.CancellationToken"));
        }

        private static void Patch(Harmony harmony, PatchDefinition definition)
        {
            try
            {
                var targetMethod = ResolveTargetMethod(definition);
                if (targetMethod == null || targetMethod.IsAbstract)
                {
                    return;
                }

                var harmonyMethod = new HarmonyMethod(definition.PatchMethod);
                harmony.Patch(
                    targetMethod,
                    definition.Kind == PatchKind.Prefix ? harmonyMethod : null,
                    definition.Kind == PatchKind.Postfix ? harmonyMethod : null);
            }
            catch (Exception ex)
            {
                LogHelper.ErrorLog(Agent.Logger, $"Error applying patch {definition.TargetTypeName}.{definition.TargetMethodName}: {ex.Message}");
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

        private static MethodInfo ResolveTargetMethod(PatchDefinition definition)
        {
            if (definition.AssemblyNames.Length == 0)
            {
                var type = ResolveLoadedType(definition.TargetTypeName);
                return type == null ? null : FindTargetMethod(type, definition);
            }

            foreach (var assemblyName in definition.AssemblyNames)
            {
                var assembly = LoadAssembly(assemblyName);
                var type = assembly == null ? null : FindTargetType(assembly, definition.TargetTypeName);
                var method = type == null ? null : FindTargetMethod(type, definition);
                if (method != null)
                {
                    return method;
                }
            }

            return null;
        }

        private static MethodInfo FindTargetMethod(Type type, PatchDefinition definition)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

            var exactMatch = methods.FirstOrDefault(m => MethodMatches(m, definition));

            if (definition.TargetParameterTypeNames.Length > 0)
            {
                return exactMatch;
            }

            return exactMatch ?? methods
                .Where(m => m.Name == definition.TargetMethodName)
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
        }

        private static bool MethodMatches(MethodInfo method, PatchDefinition definition)
        {
            if (method.Name != definition.TargetMethodName)
            {
                return false;
            }

            var parameters = method.GetParameters();
            if (parameters.Length != definition.TargetParameterTypeNames.Length)
            {
                return false;
            }

            return parameters
                .Select((parameter, index) => ParameterTypeMatches(parameter.ParameterType, definition.TargetParameterTypeNames[index]))
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
