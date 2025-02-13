using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Aikido.Zen.Core.Helpers;
using System.Runtime.CompilerServices;

namespace Aikido.Zen.DotNetCore.Patches
{
    internal static class NoSQLClientPatches
    {

        private static Harmony _harmony;
        public static void ApplyPatches(Harmony harmony)
        {
            _harmony = harmony;
            // Use reflection to get the types dynamically
            // PatchMethod(harmony, "MongoDB.Driver.IMongoDatabase", "RunCommand", "MongoDB.Driver.Command`1", "MongoDB.Driver.ReadPreference", "System.Threading.CancellationToken");
            // PatchMethod(harmony, "MongoDB.Driver.IMongoDatabase", "RunCommandAsync", "MongoDB.Driver.Command`1", "MongoDB.Driver.ReadPreference", "System.Threading.CancellationToken");

            var extType = Type.GetType("MongoDB.Driver.IMongoCollectionExtensions, MongoDB.Driver");
            if (extType == null)
            {
                Console.WriteLine("Failed to find MongoDB.Driver.IMongoCollectionExtensions type.");
                return;
            }

            var extMethods = extType.GetMethods().Where(m =>
            {
                if (m.IsAbstract)
                    return false;
                if (!m.Name.Equals("Find") && !m.Name.Equals("FindAsync"))
                    return false;
                return true;
                var parameters = m.GetParameters().Select(p => p.ParameterType);
                return parameters.Any(p => p.Name == "FilterDefinition`1");
            });

            foreach (var method in extMethods)
            {
                try
                {
                    if (method.IsGenericMethod)
                    {
                        var impl = method.MakeGenericMethod(ReflectionHelper.GetTypeFromAssembly("MongoDB.Bson", "MongoDB.Bson.BsonDocument"));
                        harmony.Patch(impl, new HarmonyMethod(typeof(NoSQLClientPatches).GetMethod(nameof(OnCommandExecuting), BindingFlags.Static | BindingFlags.NonPublic)));
                    }
                    else
                    {
                        harmony.Patch(method, new HarmonyMethod(typeof(NoSQLClientPatches).GetMethod(nameof(OnCommandExecuting), BindingFlags.Static | BindingFlags.NonPublic)));
                    }
                }
                catch (Exception ex)
                {
                    
                }
            }
        }

        private static void PatchMethod(Harmony harmony, string interfaceName, string interfaceMethodName, params string[] parameterTypeNames)
        {
        }


        private static void OnCommandExecuting(object[] __args, MethodBase __originalMethod)
        {
            // normally, args[0] is our instance, but we are patching extension methods, the we need the first argument, which is the second item in our __args.
            var assembly = __args[1].GetType().Assembly.FullName?.Split(", Culture=")[0];
            Aikido.Zen.Core.Patches.NoSQLClientPatcher.OnCommandExecuting(__args, __originalMethod, __args[1], assembly, Zen.GetContext());
        }
    }
}
