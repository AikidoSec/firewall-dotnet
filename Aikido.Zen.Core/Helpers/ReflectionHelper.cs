using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// Helper class for reflection-related operations.
    /// </summary>
    public static class ReflectionHelper
    {
        private static IDictionary<string, Type> _types;
        private static IDictionary<string, Assembly> _assemblies;

        static ReflectionHelper()
        {
            _types = new Dictionary<string, Type>();
            _assemblies = new Dictionary<string, Assembly>();
        }

        /// <summary>
        /// Attempts to load an assembly, get a type from the loaded assembly, and then get a method from the type.
        /// </summary>
        /// <param name="assemblyName">The name of the assembly to load.</param>
        /// <param name="typeName">The name of the type to get from the assembly.</param>
        /// <param name="methodName">The name of the method to get from the type.</param>
        /// <param name="parameterTypeNames">The names of the parameter types for the method.</param>
        /// <returns>The MethodInfo of the specified method, or null if not found.</returns>
        public static MethodInfo GetMethodFromAssembly(string assemblyName, string typeName, string methodName, params string[] parameterTypeNames)
        {
            // Attempt to load the assembly
            // Attempt to get the assembly from the cache, if not found, load it
            if (!_assemblies.TryGetValue(assemblyName, out var assembly))
            {
                assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == assemblyName);
                // If the assembly is not loaded, and the assembly path exists, load it
                if (File.Exists($"{assemblyName}.dll") && assembly == null)
                {
                    assembly = Assembly.LoadFrom($"{assemblyName}.dll");
                }
                if (assembly == null) return null;
                _assemblies[assemblyName] = assembly;
            }

            // Attempt to get the type from the cache, if not found, get it from the loaded assembly
            var typeKey = $"{assemblyName}.{typeName}";
            if (!_types.TryGetValue(typeKey, out var type))
            {
                type = assembly.ExportedTypes.FirstOrDefault(t => t.Name == typeName);
                if (type == null) return null;
                _types[typeKey] = type;
            }

            // Use reflection to get the method
            var method = type.GetMethods().FirstOrDefault(m => m.Name == methodName && m.GetParameters().All(p => parameterTypeNames.Any(ptn => ptn == p.ParameterType.FullName)));
            return method;
        }

        public static void ClearCache()
        {
            _types.Clear();
            _assemblies.Clear();
        }
    }
}
