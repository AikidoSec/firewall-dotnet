using System;
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
            var sinkFinalizer = GetSinkFinalizer(patchMethods);

            foreach (var patchMethod in patchMethods)
            {
                foreach (var sinkPatch in patchMethod.GetCustomAttributes<SinkTargetAttribute>())
                {
                    Patch(patchMethod, sinkPatch, sinkFinalizer);
                }
            }
        }

        private static MethodInfo GetSinkFinalizer(MethodInfo[] patchMethods)
        {
            return patchMethods.FirstOrDefault(patchMethod =>
                patchMethod.GetCustomAttributes<SinkFinalizerAttribute>().Any(finalizer => !finalizer.HasTarget));
        }

        private static void Patch(MethodInfo patchMethod, SinkTargetAttribute sinkPatch, MethodInfo sinkFinalizer)
        {
            try
            {
                var targetMethod = ResolveTargetMethod(sinkPatch);

                if (targetMethod == null)
                {
                    return;
                }

                var harmonyMethod = new HarmonyMethod(patchMethod);
                var finalizerMethod = sinkFinalizer == null ? null : new HarmonyMethod(sinkFinalizer);
                switch (sinkPatch.PatchType)
                {
                    case HarmonyPatchType.Prefix:
                        _harmony.Patch(targetMethod, prefix: harmonyMethod, finalizer: finalizerMethod);
                        break;
                    case HarmonyPatchType.Postfix:
                        _harmony.Patch(targetMethod, postfix: harmonyMethod, finalizer: finalizerMethod);
                        break;
                    case HarmonyPatchType.Finalizer:
                        _harmony.Patch(targetMethod, finalizer: harmonyMethod);
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

        private static MethodInfo ResolveTargetMethod(SinkTargetAttribute sinkPatch)
        {
            return ReflectionHelper.GetMethodFromAssembly(
                sinkPatch.AssemblyName,
                sinkPatch.TargetTypeName,
                sinkPatch.TargetMethodName,
                sinkPatch.TargetParameterTypeNames);
        }

    }
}
