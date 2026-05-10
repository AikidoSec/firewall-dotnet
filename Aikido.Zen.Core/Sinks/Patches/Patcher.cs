using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using HarmonyLib;

namespace Aikido.Zen.Core.Sinks
{
    internal static class Patcher
    {
        private const string HarmonyId = "aikido.zen";
        private static readonly Harmony _harmony = new Harmony(HarmonyId);
        private static Func<Context> _getContext = () => null;

        private static readonly Type[] PatchCatalogs =
        {
            typeof(IOPatches),
            typeof(LLMPatches),
            typeof(OutboundRequestPatches),
            typeof(ProcessExecutionPatches),
            typeof(SqlClientPatches)
        };

        internal static void PatchSinks(Func<Context> getContext)
        {
            _getContext = getContext ?? (() => null);

            foreach (var catalog in PatchCatalogs)
            {
                PatchCatalog(catalog);
            }
        }

        internal static void Unpatch()
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

        internal static void PatchCatalog(Type catalogType)
        {
            var patchMethods = catalogType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var patchMethod in patchMethods)
            {
                foreach (var sinkPatch in patchMethod.GetCustomAttributes<SinkTargetAttribute>())
                {
                    Patch(patchMethod, sinkPatch);
                }
            }
        }

        private static void Patch(MethodInfo patchMethod, SinkTargetAttribute sinkPatch)
        {
            try
            {
                var assemblyNames = string.IsNullOrEmpty(sinkPatch.AssemblyName) ? Array.Empty<string>() : new[] { sinkPatch.AssemblyName };
                var targetMethod = ResolveTargetMethod(
                    assemblyNames,
                    sinkPatch.TargetTypeName,
                    sinkPatch.TargetMethodName,
                    sinkPatch.TargetParameterTypeNames);

                if (targetMethod == null || targetMethod.IsAbstract)
                {
                    return;
                }

                var harmonyMethod = new HarmonyMethod(patchMethod);
                switch (sinkPatch.PatchType)
                {
                    case HarmonyPatchType.Prefix:
                        _harmony.Patch(targetMethod, prefix: harmonyMethod);
                        break;
                    case HarmonyPatchType.Postfix:
                        _harmony.Patch(targetMethod, postfix: harmonyMethod);
                        break;
                    default:
                        LogHelper.ErrorLog(Agent.Logger, $"Unsupported patch type {sinkPatch.PatchType} for {sinkPatch.TargetTypeName}.{sinkPatch.TargetMethodName}");
                        break;
                }
            }
            catch (Exception ex)
            {
                LogHelper.ErrorLog(Agent.Logger, $"Error applying patch {sinkPatch.TargetTypeName}.{sinkPatch.TargetMethodName}: {ex.Message}");
            }
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
