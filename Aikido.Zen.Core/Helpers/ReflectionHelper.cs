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
            // Check if assembly name is null or empty
            if (string.IsNullOrEmpty(assemblyName))
            {
                return null;
            }

            // Check if assembly name could be a path traversal attempt
            if (assemblyName.Contains(".."))
            {
                return null;
            }

            // Attempt to load the assembly
            if (!_assemblies.TryGetValue(assemblyName, out var assembly))
            {
                assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == assemblyName);


                if (assembly == null)
                {
                    // we assume the loaded dll's are in the same directory as the executing assembly.
                    // The current directory is not always the same as the executing assembly's directory, so we need to get the executing directory.
                    var executingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    var assemblyPath = Path.Combine(executingDirectory, $"{assemblyName}.dll");
                    if (File.Exists(assemblyPath))
                    {
                        assembly = Assembly.LoadFrom(assemblyPath);
                    }
                }
                if (assembly == null) return null;
                _assemblies[assemblyName] = assembly;
            }

            // Attempt to get the type from the cache, if not found, get it from the loaded assembly
            var typeKey = $"{assemblyName}.{typeName}";
            if (!_types.TryGetValue(typeKey, out var type))
            {
                type = assembly.ExportedTypes.FirstOrDefault(t => t.Name == typeName || t.FullName == typeName);

                if (type == null) return null;
                _types[typeKey] = type;
            }

            // Use reflection to get the method, make sure to check for public, internal and private methods
            var method = type
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == methodName &&
                                     m.GetParameters().Select(p => p.ParameterType.FullName).SequenceEqual(parameterTypeNames));

            // fallback to the method with the most parameters
            // this is done because in case of multiple methods with the same name, they usually wrap the one with the most parameters
            // by doing this, we reduce the risk of not being able to patch the correct method in case of library updates
            method = method ?? type
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .Where(m => m.Name == methodName)
                .OrderByDescending(m => m.GetParameters().Length)
                .FirstOrDefault();

            return method;
        }

        public static void ClearCache()
        {
            _types.Clear();
            _assemblies.Clear();
        }
    }
}
