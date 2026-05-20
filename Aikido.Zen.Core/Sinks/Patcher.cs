using System;
using System.Collections.Generic;
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
            typeof(DnsSink),
            typeof(IOSink),
            typeof(LLMSink),
            typeof(OutboundRequestSink),
            typeof(ProcessExecutionSink),
            typeof(SqlClientSink)
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
            var prefixTargets = GetPrefixTargets(patchMethods);

            foreach (var patchMethod in patchMethods)
            {
                foreach (var sinkPatch in patchMethod.GetCustomAttributes<SinkTargetAttribute>())
                {
                    if (sinkPatch is SinkFinalizerAttribute && !sinkPatch.HasTarget)
                    {
                        foreach (var prefixTarget in prefixTargets)
                        {
                            Patch(patchMethod, HarmonyPatchType.Finalizer, prefixTarget);
                        }

                        continue;
                    }

                    Patch(patchMethod, sinkPatch.PatchType, sinkPatch);
                }
            }
        }

        private static SinkPrefixAttribute[] GetPrefixTargets(MethodInfo[] patchMethods)
        {
            var targets = new List<SinkPrefixAttribute>();

            foreach (var patchMethod in patchMethods)
            {
                foreach (var prefix in patchMethod.GetCustomAttributes<SinkPrefixAttribute>())
                {
                    if (prefix.HasTarget)
                    {
                        targets.Add(prefix);
                    }
                }
            }

            return targets.ToArray();
        }

        private static void Patch(MethodInfo patchMethod, HarmonyPatchType patchType, SinkTargetAttribute sinkPatch)
        {
            try
            {
                var targetMethod = ResolveTargetMethod(sinkPatch);

                if (targetMethod == null || targetMethod.IsAbstract || !IsDeclaredOnTargetType(targetMethod, sinkPatch.TargetTypeName))
                {
                    return;
                }

                var harmonyMethod = new HarmonyMethod(patchMethod);
                switch (patchType)
                {
                    case HarmonyPatchType.Prefix:
                        _harmony.Patch(targetMethod, prefix: harmonyMethod);
                        break;
                    case HarmonyPatchType.Postfix:
                        _harmony.Patch(targetMethod, postfix: harmonyMethod);
                        break;
                    case HarmonyPatchType.Finalizer:
                        _harmony.Patch(targetMethod, finalizer: harmonyMethod);
                        break;
                    default:
                        LogHelper.ErrorLog(Agent.Logger, $"Unsupported patch type {patchType} for {sinkPatch.TargetTypeName}.{sinkPatch.TargetMethodName}");
                        break;
                }
            }
            catch (Exception ex)
            {
                LogHelper.ErrorLog(Agent.Logger, $"Error applying patch {sinkPatch.TargetTypeName}.{sinkPatch.TargetMethodName}: {ex.Message}");
            }
        }

        private static MethodInfo ResolveTargetMethod(SinkTargetAttribute sinkPatch)
        {
            return ReflectionHelper.GetMethodFromAssembly(
                sinkPatch.AssemblyName,
                sinkPatch.TargetTypeName,
                sinkPatch.TargetMethodName,
                sinkPatch.TargetParameterTypeNames);
        }

        private static bool IsDeclaredOnTargetType(MethodInfo method, string targetTypeName)
        {
            var declaringType = method.DeclaringType;
            return declaringType.FullName == targetTypeName || declaringType.Name == targetTypeName;
        }
    }
}
