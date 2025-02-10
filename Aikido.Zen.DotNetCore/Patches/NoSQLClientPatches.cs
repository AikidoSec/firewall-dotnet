using System;
using System.Linq;
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
            PatchMethod(harmony, "MongoDB.Driver.IMongoDatabase", "RunCommand", "MongoDB.Driver.Command`1", "MongoDB.Driver.ReadPreference", "System.Threading.CancellationToken");
            PatchMethod(harmony, "MongoDB.Driver.IMongoDatabase", "RunCommandAsync", "MongoDB.Driver.Command`1", "MongoDB.Driver.ReadPreference", "System.Threading.CancellationToken");
            PatchMethod(harmony, "MongoDB.Driver.IMongoCollection`1", "Find", "MongoDB.Driver.IMongoCollection`1", "MongoDB.Driver.FilterDefinition", "MongoDB.Driver.FindOptions");
            PatchMethod(harmony, "MongoDB.Driver.IMongoCollection`1", "FindAsync", "MongoDB.Driver.IMongoCollection`1", "MongoDB.Driver.FilterDefinition`1", "MongoDB.Driver.FindOptions`2", "System.Threading.CancellationToken");
        }

        private static void PatchMethod(Harmony harmony, string interfaceName, string interfaceMethodName, params string[] parameterTypeNames)
        {
            // Try to get the type directly first
            var interfaceType = Type.GetType(interfaceName + ", MongoDB.Driver");
            Console.WriteLine($"Trying to find interface type: {interfaceName}");
            Console.WriteLine($"Direct type lookup result: {interfaceType}");

            if (interfaceType == null)
            {
                // If not found, try without namespace
                var simpleInterfaceName = interfaceName.Split('.').Last();
                interfaceType = Type.GetType(simpleInterfaceName + ", MongoDB.Driver");
                Console.WriteLine($"Trying simple name: {simpleInterfaceName}");
                Console.WriteLine($"Simple name type lookup result: {interfaceType}");
            }

            if (interfaceType != null)
            {
                var implementingTypes = ReflectionHelper.GetImplementingClasses(interfaceType, "MongoDB.Driver", "MongoDB.Bson");
                Console.WriteLine($"Found {implementingTypes.Count} implementing types");
                foreach (var implementingType in implementingTypes)
                {
                    var methods = ReflectionHelper.GetMethodsFromType(implementingType, interfaceMethodName, parameterTypeNames);
                    foreach (var method in methods)
                    {
                        harmony.Patch(method, new HarmonyMethod(typeof(NoSQLClientPatches).GetMethod(nameof(OnCommandExecuting), BindingFlags.Static | BindingFlags.NonPublic)));
                    }
                }
            }
            else
            {
                Console.WriteLine("Failed to find interface type");
                // Try to list all loaded assemblies to help diagnose
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a => a.FullName.Contains("MongoDB")))
                {
                    Console.WriteLine($"Loaded MongoDB assembly: {assembly.FullName}");
                }
            }
        }

        private static bool OnCommandExecuting(object[] __args, MethodBase __originalMethod, object __instance)
        {
            var assembly = __instance.GetType().Assembly.FullName?.Split(", Culture=")[0];
            return Aikido.Zen.Core.Patches.NoSQLClientPatcher.OnCommandExecuting(__args, __originalMethod, __instance, assembly, Zen.GetContext());
        }
    }
}
