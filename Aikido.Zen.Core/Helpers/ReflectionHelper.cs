using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// Helper class for reflection-related operations.
    /// </summary>
    public static class ReflectionHelper
    {
        /// <summary>
        /// Attempts to load an assembly, get a type from the loaded assembly, and then get a method from the type.
        /// </summary>
        public static MethodInfo GetMethodFromAssembly(string assemblyName, string typeName, string methodName, params string[] parameterTypeNames)
        {
            var assembly = AssemblyHelper.GetAssembly(assemblyName);
            if (assembly == null) return null;

            var type = TypeReflectionHelper.GetTypeFromAssembly(assembly, typeName);
            if (type == null) return null;

            return TypeReflectionHelper.GetMethodsFromType(type, methodName, parameterTypeNames).FirstOrDefault();
        }

        /// <summary>
        /// Returns all classes that implement the specified interface within the given assemblies.
        /// </summary>
        public static List<Type> GetImplementingClasses(Type interfaceType, params string[] assemblyNames)
        {
            var assemblies = AssemblyHelper.GetAssemblies(assemblyNames);
            return TypeReflectionHelper.GetImplementingClasses(interfaceType, assemblies);
        }

        /// <summary>
        /// Returns all classes that implement the specified interface within the given assemblies.
        /// </summary>
        public static List<Type> GetImplementingClasses(string fullTypeName, params string[] assemblyNames)
        {
            // First try to get the type directly
            var type = Type.GetType(fullTypeName);

            // If not found, search through our loaded assemblies
            if (type == null)
            {
                var assemblies = AssemblyHelper.GetAssemblies(assemblyNames);
                type = assemblies
                    .SelectMany(a => a.GetTypes())
                    .FirstOrDefault(t => t.FullName == fullTypeName);
            }

            if (type == null)
            {
                return new List<Type>();
            }

            return GetImplementingClasses(type, assemblyNames);
        }

        /// <summary>
        /// Retrieves all methods from the specified type by its name, including generic methods.
        /// </summary>
        public static IEnumerable<MethodInfo> GetMethodsFromType(Type type, string methodName, params string[] parameterTypeNames)
        {
            return TypeReflectionHelper.GetMethodsFromType(type, methodName, parameterTypeNames);
        }

        /// <summary>
        /// Clears all caches (types, assemblies, generic implementations, and methods).
        /// </summary>
        public static void ClearCache()
        {
            AssemblyHelper.ClearCache();
            TypeReflectionHelper.ClearCache();
        }

        /// <summary>
        /// Forces a refresh of all caches and rescans for implementations.
        /// </summary>
        public static void RefreshCache()
        {
            AssemblyHelper.RefreshCache();
            TypeReflectionHelper.ClearCache();
        }
    }
}
