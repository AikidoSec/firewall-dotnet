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
        private static readonly object PatchLock = new object();
        private static Func<Context> _getContext = () => null;
        private static bool _sinksPatched;

        private static readonly Type[] PatchCatalogs =
        {
            typeof(IOSink),
            typeof(LLMSink),
            typeof(OutboundRequestInnerSink),
            typeof(OutboundRequestSink),
            typeof(ProcessExecutionSink),
            typeof(SqlClientSink)
        };

        internal static void PatchSinks(Func<Context> getContext)
        {
            lock (PatchLock)
            {
                _getContext = getContext ?? (() => null);

                // Harmony patches are process-wide. Even though Zen.Start replaces the
                // Agent singleton, repatching would register another prefix/finalizer
                // and cause the same sink to run multiple times for one runtime call.
                if (_sinksPatched)
                {
                    return;
                }

                foreach (var catalog in PatchCatalogs)
                {
                    PatchCatalog(catalog);
                }

                _sinksPatched = true;
            }
        }

        internal static void Unpatch()
        {
            lock (PatchLock)
            {
                if (Harmony.HasAnyPatches(HarmonyId))
                {
                    _harmony.UnpatchAll(HarmonyId);
                }

                _sinksPatched = false;
                _getContext = () => null;
            }
        }

        internal static Context GetContext()
        {
            return _getContext();
        }

        internal static void PatchCatalog(Type catalogType)
        {
            var patchMethods = catalogType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var commonFinalizer = GetCommonFinalizer(patchMethods);

            foreach (var patchMethod in patchMethods)
            {
                foreach (var sinkPatch in patchMethod.GetCustomAttributes<SinkTargetAttribute>())
                {
                    Patch(patchMethod, sinkPatch);

                    if (commonFinalizer != null)
                    {
                        AddFinalizerPatch(commonFinalizer, sinkPatch);
                    }
                }
            }
        }

        private static MethodInfo GetCommonFinalizer(MethodInfo[] patchMethods)
        {
            return patchMethods.FirstOrDefault(patchMethod =>
                patchMethod.GetCustomAttributes<SinkFinalizerAttribute>().Any(finalizer => !finalizer.HasTarget));
        }

        private static void AddFinalizerPatch(MethodInfo commonFinalizer, SinkTargetAttribute sinkPatch)
        {
            var finalizerPatch = new SinkFinalizerAttribute(
                sinkPatch.AssemblyName,
                sinkPatch.TargetTypeName,
                sinkPatch.TargetMethodName,
                sinkPatch.TargetParameterTypeNames);

            Patch(commonFinalizer, finalizerPatch);
        }

        private static void Patch(MethodInfo patchMethod, SinkTargetAttribute sinkPatch)
        {
            try
            {
                var targetMethod = ResolveTargetMethod(sinkPatch);

                if (targetMethod == null)
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
