using System.Reflection;
using HarmonyLib;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.DotNetCore.Patches
{
    internal static class NoSQLClientPatches
    {
        public static void ApplyPatches(Harmony harmony)
        {
            // Use reflection to get the types dynamically
            PatchMethod(harmony, "MongoDB.Driver.IMongoDatabase", "RunCommand");
            PatchMethod(harmony, "MongoDB.Driver.IMongoDatabase", "RunCommandAsync");
            PatchMethod(harmony, "MongoDB.Driver.IMongoCollection`1", "Find");
            PatchMethod(harmony, "MongoDB.Driver.IMongoCollection`1", "FindAsync");
        }

        private static void PatchMethod (Harmony harmony, string interfaceName, string interfaceMethodName)
        {
            var implementingTypes = ReflectionHelper.GetImplementingClasses(interfaceName, "MongoDB.Driver", "MongoDb.Bson");
            foreach (var implementingType in implementingTypes)
            {
               var method = ReflectionHelper.GetMethodFromAssembly(implementingType.Assembly.GetName().Name, implementingType.Name, interfaceMethodName);
               if (method != null)
               {
                   harmony.Patch(method, new HarmonyMethod(typeof(NoSQLClientPatches).GetMethod(nameof(OnCommandExecuting), BindingFlags.Static | BindingFlags.NonPublic)));
               }
            }
        }

        private static void PatchMethod (Harmony harmony, string assemblyName, string typeName, string methodName, params string[] parameterTypeNames)
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
