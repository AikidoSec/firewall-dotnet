using System;
using System.Reflection;
using HarmonyLib;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.DotNetCore.Patches
{
    internal static class NoSQLClientPatches
    {
        public static void ApplyPatches(Harmony harmony)
        {
            // Use reflection to get the types dynamically
            PatchMethod(harmony, "MongoDB.Driver", "MongoCollection", "Find");
            PatchMethod(harmony, "MongoDB.Driver", "MongoCollection", "InsertOne");
            PatchMethod(harmony, "MongoDB.Driver", "MongoCollection", "UpdateOne");
            PatchMethod(harmony, "MongoDB.Driver", "MongoCollection", "DeleteOne");
            PatchMethod(harmony, "MongoDB.Driver", "MongoCollection", "Aggregate");
        }

        private static void PatchMethod(Harmony harmony, string assemblyName, string typeName, string methodName, params string[] parameterTypeNames)
        {
            var method = ReflectionHelper.GetMethodFromAssembly(assemblyName, typeName, methodName, parameterTypeNames);
            if (method != null)
            {
                harmony.Patch(method, new HarmonyMethod(typeof(NoSQLClientPatches).GetMethod(nameof(OnCommandExecuting), BindingFlags.Static | BindingFlags.NonPublic)));
            }
        }

        private static bool OnCommandExecuting(object[] __args, MethodBase __originalMethod, object __instance)
        {
            var assembly = __instance.GetType().Assembly.FullName?.Split(", Culture=")[0];
            return Aikido.Zen.Core.Patches.NoSQLClientPatcher.OnCommandExecuting(__args, __originalMethod, __instance, assembly, Zen.GetContext());
        }
    }
}
