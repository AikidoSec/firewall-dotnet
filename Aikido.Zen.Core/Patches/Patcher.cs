using System;
using System.Collections.Generic;
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
                var harmony = new Harmony(harmonyId);
                ApplyPatches(harmony, getContext);
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

        public static bool CanPatch(out string message)
        {
            message = "";
            return true;
        }

        internal static void ApplyPatches(Harmony harmony, Func<Context> getContext)
        {
            _getContext = getContext ?? (() => null);

            foreach (var patchMethod in GetPatchMethods())
            {
                foreach (var target in GetPatchTargets(patchMethod))
                {
                    ApplyPatch(harmony, patchMethod, target);
                }
            }
        }

        internal static Context GetContext()
        {
            return _getContext();
        }

        internal static PatchTargetAttribute GetPatchTarget(MethodBase originalMethod)
        {
            return originalMethod != null && PatchTargetsByMethod.TryGetValue(originalMethod, out var target)
                ? target
                : null;
        }

        private static readonly IDictionary<MethodBase, PatchTargetAttribute> PatchTargetsByMethod = new Dictionary<MethodBase, PatchTargetAttribute>();

        private static void ApplyPatch(Harmony harmony, MethodInfo patchMethod, PatchTargetAttribute target)
        {
            try
            {
                var targetMethod = ResolveTargetMethod(target);
                if (targetMethod == null || targetMethod.IsAbstract)
                {
                    return;
                }

                var harmonyMethod = new HarmonyMethod(patchMethod);
                harmony.Patch(
                    targetMethod,
                    target.Kind == PatchKind.Prefix ? harmonyMethod : null,
                    target.Kind == PatchKind.Postfix ? harmonyMethod : null);

                PatchTargetsByMethod[targetMethod] = target;
            }
            catch (Exception ex)
            {
                LogHelper.ErrorLog(Agent.Logger, $"Error applying patch {patchMethod.DeclaringType?.FullName}.{patchMethod.Name}: {ex.Message}");
            }
        }

        private static MethodInfo ResolveTargetMethod(PatchTargetAttribute target)
        {
            foreach (var assemblyName in target.AssemblyNames)
            {
                var type = ResolveTargetTypeFromAssembly(target, assemblyName);
                var method = type == null ? null : ResolveTargetMethod(target, type);
                if (method != null)
                {
                    return method;
                }
            }

            var fallbackType = ResolveTargetTypeFromLoadedAssemblies(target);
            return fallbackType == null ? null : ResolveTargetMethod(target, fallbackType);
        }

        private static MethodInfo ResolveTargetMethod(PatchTargetAttribute target, Type type)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

            var exactMatch = methods.FirstOrDefault(m =>
                m.Name == target.TargetMethodName &&
                m.GetParameters().Select(p => p.ParameterType.FullName).SequenceEqual(target.TargetParameterTypeNames));

            if (target.TargetParameterTypeNames.Length > 0)
            {
                return exactMatch;
            }

            return exactMatch ?? methods
                .Where(m => m.Name == target.TargetMethodName)
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();
        }

        private static Type ResolveTargetTypeFromAssembly(PatchTargetAttribute target, string assemblyName)
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

            return FindTargetType(target, assembly);
        }

        private static Type ResolveTargetTypeFromLoadedAssemblies(PatchTargetAttribute target)
        {
            if (string.IsNullOrEmpty(target.TargetTypeName))
            {
                return null;
            }

            return Type.GetType(target.TargetTypeName) ??
                AppDomain.CurrentDomain
                    .GetAssemblies()
                    .Select(assembly => FindTargetType(target, assembly))
                    .FirstOrDefault(type => type != null);
        }

        private static Type FindTargetType(PatchTargetAttribute target, Assembly assembly)
        {
            try
            {
                return assembly.GetTypes().FirstOrDefault(t => t.FullName == target.TargetTypeName || t.Name == target.TargetTypeName);
            }
            catch
            {
                return null;
            }
        }

        private static IEnumerable<MethodInfo> GetPatchMethods()
        {
            return typeof(Patcher).Assembly
                .GetTypes()
                .SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                .Where(method => GetPatchTargets(method).Any());
        }

        private static IEnumerable<PatchTargetAttribute> GetPatchTargets(MethodInfo method)
        {
            return method.GetCustomAttributes(typeof(PatchTargetAttribute), false).Cast<PatchTargetAttribute>();
        }
    }
}
