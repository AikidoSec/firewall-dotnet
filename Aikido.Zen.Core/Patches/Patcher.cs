using System;
using System.Linq;
using System.Reflection;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using HarmonyLib;

namespace Aikido.Zen.Core.Patches
{
    public class Patcher
    {
        private const string DefaultHarmonyId = "aikido.zen";
        private static Func<Context> _getContext = () => null;

        public static void Patch()
        {
            Patch(DefaultHarmonyId, () => null);
        }

        public static void Patch(string harmonyId, Func<Context> getContext)
        {
            try
            {
                Patch(new Harmony(harmonyId), getContext);
            }
            catch (Exception ex)
            {
                LogHelper.ErrorLog(Agent.Logger, $"Error patching: {ex.Message}");
            }
        }

        public static void Unpatch()
        {
            Unpatch(DefaultHarmonyId);
        }

        public static void Unpatch(string harmonyId)
        {
            if (Harmony.HasAnyPatches(harmonyId))
            {
                var harmony = new Harmony(harmonyId);
                harmony.UnpatchAll(harmonyId);
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
            Patch(harmony, PatchDefinition.Prefix(SinkKind.IOPath, "", "System.IO.File", "Open", "System.String", "System.IO.FileMode"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.IOPath, "", "System.IO.File", "OpenRead", "System.String"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.IOPath, "", "System.IO.File", "OpenWrite", "System.String"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.IOPath, "", "System.IO.File", "Create", "System.String", "System.Int32", "System.IO.FileOptions"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.IOPath, "", "System.IO.File", "Delete", "System.String"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.IOTwoPaths, "", "System.IO.File", "Copy", "System.String", "System.String", "System.Boolean"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.IOTwoPaths, "", "System.IO.File", "Move", "System.String", "System.String"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.IOTwoPaths, "", "System.IO.File", "Move", "System.String", "System.String", "System.Boolean"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.IOPath, "", "System.IO.File", "ReadAllText", "System.String"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.IOPath, "", "System.IO.File", "ReadAllBytes", "System.String"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.IOPath, "", "System.IO.File", "WriteAllText", "System.String", "System.String"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.IOPath, "", "System.IO.File", "WriteAllBytes", "System.String", "System.Byte[]"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.IOPath, "", "System.IO.File", "AppendAllText", "System.String", "System.String"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.IOPath, "", "System.IO.Path", "GetFullPath", "System.String"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.IOTwoPaths, "", "System.IO.Path", "GetFullPath", "System.String", "System.String"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.IOPath, "", "System.IO.Directory", "CreateDirectory", "System.String"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.IOPath, "", "System.IO.Directory", "CreateDirectory", "System.String", "System.Security.AccessControl.DirectorySecurity"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.IOPath, "", "System.IO.Directory", "Delete", "System.String", "System.Boolean"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.IOPath, "", "System.IO.Directory", "GetFiles", "System.String"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.IOPath, "", "System.IO.Directory", "GetFiles", "System.String", "System.String"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.IOPath, "", "System.IO.Directory", "GetFiles", "System.String", "System.String", "System.IO.SearchOption"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.IOPath, "", "System.IO.Directory", "GetDirectories", "System.String"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.IOPath, "", "System.IO.Directory", "GetDirectories", "System.String", "System.String"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.IOPath, "", "System.IO.Directory", "GetDirectories", "System.String", "System.String", "System.IO.SearchOption"));

            Patch(harmony, PatchDefinition.Postfix(SinkKind.LLM, "OpenAI", "OpenAI.Chat.ChatClient", "CompleteChat"));
            Patch(harmony, PatchDefinition.Postfix(SinkKind.LLM, "OpenAI", "OpenAI.Chat.ChatClient", "CompleteChatAsync"));
            Patch(harmony, PatchDefinition.Postfix(SinkKind.LLM, "Rystem.OpenAi", "Rystem.OpenAi.Chat.OpenAiChat", "ExecuteAsync"));
            Patch(harmony, PatchDefinition.Postfix(SinkKind.LLM, "Rystem.OpenAi", "Rystem.OpenAi.Chat.OpenAiChat", "ExecuteAsStreamAsync"));

            Patch(harmony, PatchDefinition.Prefix(SinkKind.OutboundRequest, "System.Net.Http", "HttpClient", "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Net.Http.HttpCompletionOption", "System.Threading.CancellationToken"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.OutboundRequest, "System.Net.Http", "HttpClient", "SendAsync", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.OutboundRequest, "System.Net.Http", "HttpClient", "Send", "System.Net.Http.HttpRequestMessage", "System.Threading.CancellationToken"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.OutboundRequest, "", "System.Net.WebRequest", "GetResponse"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.OutboundRequest, "", "System.Net.HttpWebRequest", "GetResponse"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.OutboundRequest, "", "System.Net.WebRequest", "GetResponseAsync"));

            Patch(harmony, PatchDefinition.Prefix(SinkKind.ProcessExecution, new[] { "System.Diagnostics.Process", "System" }, "System.Diagnostics.Process", "Start"));

            Patch(harmony, PatchDefinition.Prefix(SinkKind.SqlClient, "System.Data.Common", "DbCommand", "ExecuteNonQueryAsync"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.SqlClient, "System.Data.Common", "DbCommand", "ExecuteReaderAsync", "System.Data.CommandBehavior"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.SqlClient, "System.Data.Common", "DbCommand", "ExecuteScalarAsync"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.SqlClient, "Microsoft.Data.SqlClient", "SqlCommand", "ExecuteNonQuery"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.SqlClient, "Microsoft.Data.SqlClient", "SqlCommand", "ExecuteScalar"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.SqlClient, "Microsoft.Data.SqlClient", "SqlCommand", "ExecuteReader", "System.Data.CommandBehavior"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.SqlClient, "System.Data.SqlClient", "SqlCommand", "ExecuteNonQuery"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.SqlClient, "System.Data.SqlClient", "SqlCommand", "ExecuteScalar"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.SqlClient, "System.Data.SqlClient", "SqlCommand", "ExecuteReader", "System.Data.CommandBehavior"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.SqlClient, "System.Data.SqlServerCe", "SqlCeCommand", "ExecuteNonQuery"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.SqlClient, "System.Data.SqlServerCe", "SqlCeCommand", "ExecuteScalar"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.SqlClient, "System.Data.SqlServerCe", "SqlCeCommand", "ExecuteReader", "System.Data.CommandBehavior"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.SqlClient, "Microsoft.Data.Sqlite", "SqliteCommand", "ExecuteNonQuery"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.SqlClient, "Microsoft.Data.Sqlite", "SqliteCommand", "ExecuteScalar"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.SqlClient, "Microsoft.Data.Sqlite", "SqliteCommand", "ExecuteReader", "System.Data.CommandBehavior"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.SqlClient, "MySql.Data", "MySqlClient.MySqlCommand", "ExecuteNonQuery"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.SqlClient, "MySql.Data", "MySqlClient.MySqlCommand", "ExecuteScalar"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.SqlClient, "MySql.Data", "MySqlClient.MySqlCommand", "ExecuteReader", "System.Data.CommandBehavior"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.SqlClient, "MySqlConnector", "MySqlCommand", "ExecuteNonQuery"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.SqlClient, "MySqlConnector", "MySqlCommand", "ExecuteScalar"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.SqlClient, "MySqlConnector", "MySqlCommand", "ExecuteReader", "System.Data.CommandBehavior"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.SqlClient, "Npgsql", "NpgsqlCommand", "ExecuteNonQuery"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.SqlClient, "Npgsql", "NpgsqlCommand", "ExecuteScalar"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.SqlClient, "Npgsql", "NpgsqlCommand", "ExecuteReader", "System.Data.CommandBehavior"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.SqlClient, "Npgsql", "NpgsqlCommand", "ExecuteNonQueryAsync", "System.Threading.CancellationToken"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.SqlClient, "Npgsql", "NpgsqlCommand", "ExecuteReaderAsync", "System.Threading.CancellationToken"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.SqlClient, "Npgsql", "NpgsqlCommand", "ExecuteReaderAsync", "System.Data.CommandBehavior", "System.Threading.CancellationToken"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.SqlClient, "Npgsql", "NpgsqlCommand", "ExecuteScalarAsync", "System.Threading.CancellationToken"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.SqlClient, "MySqlX", "XDevAPI.Relational.Table", "Select"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.SqlClient, "MySqlX", "XDevAPI.Relational.Table", "Insert"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.SqlClient, "MySqlX", "XDevAPI.Relational.Table", "Update"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.SqlClient, "MySqlX", "XDevAPI.Relational.Table", "Delete"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.SqlClient, "NPoco", "Database", "ExecuteReaderHelper", "System.Data.Common.DbCommand"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.SqlClient, "NPoco", "Database", "ExecuteNonQueryHelper", "System.Data.Common.DbCommand"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.SqlClient, "NPoco", "Database", "ExecuteScalarHelper", "System.Data.Common.DbCommand"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.SqlClient, "Microsoft.EntityFrameworkCore.Relational", "RelationalDatabaseFacadeExtensions", "ExecuteSqlRaw", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade", "System.String", "System.Collections.Generic.IEnumerable`1[System.Object]"));
            Patch(harmony, PatchDefinition.Prefix(SinkKind.SqlClient, "Microsoft.EntityFrameworkCore.Relational", "RelationalDatabaseFacadeExtensions", "ExecuteSqlRawAsync", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade", "System.String", "System.Collections.Generic.IEnumerable`1[System.Object]", "System.Threading.CancellationToken"));
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

                var harmonyMethod = GetPatchMethod(definition);
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

        private static HarmonyMethod GetPatchMethod(PatchDefinition definition)
        {
            if (definition.Kind == PatchKind.Postfix && definition.Sink == SinkKind.LLM)
            {
                return GetHarmonyMethod(typeof(LLMSink), nameof(LLMSink.OnLLMCallCompleted), typeof(object[]), typeof(MethodBase), typeof(object), typeof(object));
            }

            switch (definition.Sink)
            {
                case SinkKind.IOPath:
                    return GetHarmonyMethod(typeof(IOSink), nameof(IOSink.OnPathOperation), typeof(object[]), typeof(MethodBase));
                case SinkKind.IOTwoPaths:
                    return GetHarmonyMethod(typeof(IOSink), nameof(IOSink.OnTwoPathOperation), typeof(object[]), typeof(MethodBase));
                case SinkKind.OutboundRequest:
                    return GetHarmonyMethod(typeof(OutboundRequestSink), nameof(OutboundRequestSink.OnRequest), typeof(object[]), typeof(MethodBase), typeof(object));
                case SinkKind.ProcessExecution:
                    return GetHarmonyMethod(typeof(ProcessExecutionSink), nameof(ProcessExecutionSink.OnProcessStart), typeof(object[]), typeof(MethodBase), typeof(object));
                case SinkKind.SqlClient:
                    return GetHarmonyMethod(typeof(SqlClientSink), nameof(SqlClientSink.OnCommandExecuting), typeof(object[]), typeof(MethodBase), typeof(object));
                default:
                    throw new InvalidOperationException($"No patch method registered for sink {definition.Sink}.");
            }
        }

        private static HarmonyMethod GetHarmonyMethod(Type type, string methodName, params Type[] parameterTypes)
        {
            var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, parameterTypes, null);
            if (method == null)
            {
                throw new MissingMethodException(type.FullName, methodName);
            }

            return new HarmonyMethod(method);
        }

        private static MethodInfo ResolveTargetMethod(PatchDefinition definition)
        {
            foreach (var assemblyName in definition.AssemblyNames)
            {
                var type = ResolveTargetTypeFromAssembly(definition, assemblyName);
                var method = type == null ? null : ResolveTargetMethod(definition, type);
                if (method != null)
                {
                    return method;
                }
            }

            var fallbackType = ResolveTargetTypeFromLoadedAssemblies(definition);
            return fallbackType == null ? null : ResolveTargetMethod(definition, fallbackType);
        }

        private static MethodInfo ResolveTargetMethod(PatchDefinition definition, Type type)
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

        private static Type ResolveTargetTypeFromAssembly(PatchDefinition definition, string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName) || assemblyName.Contains(".."))
            {
                return null;
            }

            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == assemblyName);
            if (assembly == null)
            {
                try
                {
                    assembly = Assembly.Load(new AssemblyName(assemblyName));
                }
                catch
                {
                    return null;
                }
            }

            return FindTargetType(definition, assembly);
        }

        private static Type ResolveTargetTypeFromLoadedAssemblies(PatchDefinition definition)
        {
            if (string.IsNullOrEmpty(definition.TargetTypeName))
            {
                return null;
            }

            return Type.GetType(definition.TargetTypeName) ??
                AppDomain.CurrentDomain
                    .GetAssemblies()
                    .Select(assembly => FindTargetType(definition, assembly))
                    .FirstOrDefault(type => type != null);
        }

        private static Type FindTargetType(PatchDefinition definition, Assembly assembly)
        {
            try
            {
                return assembly.GetTypes().FirstOrDefault(t => t.FullName == definition.TargetTypeName || t.Name == definition.TargetTypeName);
            }
            catch
            {
                return null;
            }
        }
    }
}
