using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

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
            if (!_assemblies.TryGetValue(assemblyName, out var assembly))
            {
                assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == assemblyName);
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
                type = assembly.ExportedTypes.FirstOrDefault(t => t.Name == typeName || t.FullName == typeName);

                if (type == null) return null;
                _types[typeKey] = type;
            }

            // Use reflection to get the method, make sure to check for public, internal and private methods
            var method = type
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == methodName &&
                                     (!parameterTypeNames.Any() || m.GetParameters().Select(p => p.ParameterType.FullName).SequenceEqual(parameterTypeNames)) &&
                                     m.IsGenericMethod == false
                                    );
            return method;
        }

        /// <summary>
        /// Returns all classes that implement the specified interface within the given assemblies.
        /// </summary>
        /// <param name="interfaceType">The interface type to search for.</param>
        /// <param name="assemblyNames">The names of the assemblies to search within.</param>
        /// <returns>A list of types that implement the specified interface.</returns>
        public static List<Type> GetImplementingClasses(Type interfaceType, params string[] assemblyNames)
        {
            var implementingTypes = new List<Type>();

            foreach (var assemblyName in assemblyNames)
            {
                if (!_assemblies.TryGetValue(assemblyName, out var assembly))
                {
                    assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == assemblyName);
                    if (File.Exists($"{assemblyName}.dll") && assembly == null)
                    {
                        assembly = Assembly.LoadFrom($"{assemblyName}.dll");
                    }
                    if (assembly == null) continue;
                    _assemblies[assemblyName] = assembly;
                }

                // Find all types in the assembly that implement the specified interface
                var types = assembly.DefinedTypes.Where(t => t.IsClass && !t.IsAbstract)
                    .Where(t => t.GetInterfaces().Any(i => (i.IsGenericType && i.GetGenericTypeDefinition() == interfaceType) || i.IsAssignableFrom(t)));
                implementingTypes.AddRange(types);
            }

            return implementingTypes;
        }

        /// <summary>
        /// Returns all classes that implement the specified interface within the given assemblies.
        /// </summary>
        /// <param name="fullTypeName">The full name of the type to search for.</param>
        /// <param name="assemblyNames">The names of the assemblies to search within.</param>
        /// <returns>A list of types that implement the specified interface.</returns>
        public static List<Type> GetImplementingClasses(string fullTypeName, params string[] assemblyNames)
        {
            var type = Type.GetType(fullTypeName) ??
                AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => a.GetTypes())
                    .FirstOrDefault(t => t.FullName == fullTypeName);
            if (type == null)
            {
                return new List<Type>();
            }
            return GetImplementingClasses(type, assemblyNames);
        }

        /// <summary>
        /// Retrieves all methods from the specified type by their name.
        /// </summary>
        /// <param name="type">The type to search for the methods.</param>
        /// <param name="methodName">The name of the methods to find.</param>
        /// <returns>A list of MethodInfo objects representing the methods if found; otherwise, an empty list.</returns>
        public static List<MethodInfo> GetMethodsFromType(Type type, string methodName)
        {
            if (type == null || string.IsNullOrEmpty(methodName))
            {
                return new List<MethodInfo>();
            }

            // Search for all methods with the specified name in the given type
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                              .Where(m => m.Name == methodName)
                              .ToList();

            return methods;
        }



        public static void ClearCache()
        {
            _types.Clear();
            _assemblies.Clear();
        }
    }
}
