using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Patches;

namespace Aikido.Zen.DotNetFramework.Patches
{
    /// <summary>
    /// Patches methods related to process execution to prevent shell injection attacks.
    /// </summary>
    internal static class ProcessPatches
    {
        /// <summary>
        /// Applies patches to methods and properties of the Process and ProcessStartInfo classes.
        /// </summary>
        /// <param name="harmony">The Harmony instance used for patching.</param>
        public static void ApplyPatches(Harmony harmony)
        {
            // Patch Process.Start() method
            PatchMethod(harmony, "System", "Process", "Start");

            // Patch properties of ProcessStartInfo
            PatchMethod(harmony, "System", "ProcessStartInfo", "FileName");
            PatchMethod(harmony, "System", "ProcessStartInfo", "Arguments");
            PatchMethod(harmony, "System", "ProcessStartInfo", "UseShellExecute");
        }

        private static void PatchMethod(Harmony harmony, string assemblyName, string typeName, string methodName)
        {
            var method = ReflectionHelper.GetMethodFromAssembly(assemblyName, typeName, methodName);
            if (method != null)
            {
                harmony.Patch(method, new HarmonyMethod(typeof(ProcessPatches).GetMethod(nameof(OnProcessStart), BindingFlags.Static | BindingFlags.NonPublic)));
            }
        }

        private static bool OnProcessStart(object[] __args, MethodBase __originalMethod, object __instance)
        {
            var context = Zen.GetContext();
            return ProcessExecutionPatcher.OnProcessStart(__args, __originalMethod, __instance, context);
        }
    }
}
