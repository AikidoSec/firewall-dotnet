using System;
using System.Reflection;
using HarmonyLib;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.DotNetFramework.Patches
{
    internal static class NoSQLClientPatches
    {
        /// <summary>
        /// Applies patches to NoSQL command methods using Harmony.
        /// </summary>
        /// <param name="harmony">The Harmony instance used for patching.</param>
        public static void ApplyPatches(Harmony harmony)
        {
            // Use reflection to get the types dynamically
            PatchMethod(harmony, "MongoDB.Driver", "MongoCollection", "Find");
            PatchMethod(harmony, "MongoDB.Driver", "MongoCollection", "InsertOne");
            PatchMethod(harmony, "MongoDB.Driver", "MongoCollection", "UpdateOne");
            PatchMethod(harmony, "MongoDB.Driver", "MongoCollection", "DeleteOne");
            PatchMethod(harmony, "MongoDB.Driver", "MongoCollection", "Aggregate");
        }

        /// <summary>
        /// Patches a method using Harmony by dynamically retrieving it via reflection.
        /// </summary>
        /// <param name="harmony">The Harmony instance used for patching.</param>
        /// <param name="assemblyName">The name of the assembly containing the type.</param>
        /// <param name="typeName">The name of the type containing the method.</param>
        /// <param name="methodName">The name of the method to patch.</param>
        /// <param name="parameterTypeNames">The names of the parameter types for the method.</param>
        private static void PatchMethod(Harmony harmony, string assemblyName, string typeName, string methodName, params string[] parameterTypeNames)
        {
            var method = ReflectionHelper.GetMethodFromAssembly(assemblyName, typeName, methodName, parameterTypeNames);
            if (method != null)
            {
                harmony.Patch(method, new HarmonyMethod(typeof(NoSQLClientPatches).GetMethod(nameof(OnCommandExecuting), BindingFlags.Static | BindingFlags.NonPublic)));
            }
        }

        /// <summary>
        /// Callback method executed before the original command method is executed.
        /// </summary>
        /// <param name="__args">The arguments passed to the original method.</param>
        /// <param name="__originalMethod">The original method being patched.</param>
        /// <param name="__instance">The instance of the command being executed.</param>
        /// <returns>True if the original method should continue execution; otherwise, false.</returns>
        private static bool OnCommandExecuting(object[] __args, MethodBase __originalMethod, object __instance)
        {
            var assembly = __instance.GetType().Assembly.FullName?.Split(new[] { ", Culture=" }, StringSplitOptions.RemoveEmptyEntries)[0];
            return Aikido.Zen.Core.Patches.NoSQLClientPatcher.OnCommandExecuting(__args, __originalMethod, __instance, assembly, Zen.GetContext());
        }
    }
}
