using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
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
                // first we look in ExportedTypes, since it's faster than GetTypes(), but doesn't include all types
                // if we don't find the type, we fallback to GetTypes()
                type = assembly.ExportedTypes.FirstOrDefault(t => t.Name == typeName || t.FullName == typeName)
                    ?? assembly.GetTypes().FirstOrDefault(t => t.Name == typeName || t.FullName == typeName);

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

        /// <summary>
        /// This method is crucial for preventing re-entrancy issues with certain IL weavers and patchers.
        /// </summary>
        /// <param name="assemblyFullName">The full name of the assembly to check.</param>
        /// <returns>True if the assembly should be excluded from patching, false otherwise.</returns>
        public static bool ShouldExcludeAssembly(Assembly assembly)
        {
            if (assembly == null)
                return false;

            // Exclude assemblies with GeneratedCodeAttribute
            var customAttributes = assembly.GetCustomAttributes(typeof(GeneratedCodeAttribute), false);
            if (customAttributes != null && customAttributes.Length > 0)
                return true;

            // Exclude DispatchProxy-based dynamic types
            var assemblyFullName = assembly.FullName ?? string.Empty;
            if (assemblyFullName.IndexOf("Anonymously Hosted DynamicMethods", StringComparison.OrdinalIgnoreCase) >= 0 || //.NET runtime to hold dynamically generated method, it usually appears when the runtime uses Reflection.Emit
                assemblyFullName.IndexOf("DynamicProxyGenAssembly2", StringComparison.OrdinalIgnoreCase) >= 0) // default assembly name used by Castle DynamicProxy, the proxy generator behind many interceptors
            {
                return true;
            }

            // Exclude assemblies that are known to cause issues
            // Using FullName which includes version info, so we check if it contains the assembly name
            var excludedAssemblies = new[]
            {
                // IL weaving / patching
                "Costura",
                "Harmony",
                "Fody",
                "Mono.Cecil",
                "PostSharp",
                "dnlib",
                "ILRepack",
                "BepInEx",

                // Dynamic proxy / AOP
                "Castle.DynamicProxy",
                "Autofac.Extras.DynamicProxy",
                "DispatchProxy",

                // Instrumentation / APM
                "Datadog.Trace",
                "NewRelic",
                "AppDynamics",
                "OpenTelemetry",

                // Scripting / dynamic compilation
                "Microsoft.CodeAnalysis",
                "Roslyn",
                "ScriptCs",
                "Jint",
                "ClearScript",

                // System.Reflection.Emit (dynamic runtime code generation)
                "System.Reflection.Emit"
            };

            if (excludedAssemblies.Any(excluded =>
                assemblyFullName.IndexOf(excluded, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return true;
            }

            return false;
        }
        /// <summary>
        /// Retrieves the calling assembly by examining the stack trace.
        /// </summary>
        /// <returns>The calling assembly</returns>
        public static Assembly GetCallingAssembly()
        {
            try
            {
                var stackTrace = new StackTrace();
                var frames = stackTrace.GetFrames();

                Assembly[] assemblies = new Assembly[frames.Length];
                for (int i = 0; i < frames.Length; i++)
                {
                    var method = frames[i].GetMethod();
                    var assembly = method?.DeclaringType?.Assembly;
                    assemblies[i] = assembly;
                }
                // Skip the first few frames which are our patch methods
                for (int i = 2; i < frames.Length; i++)
                {
                    var method = frames[i].GetMethod();
                    var assembly = method?.DeclaringType?.Assembly;
                    if (assembly != null)
                    {
                        return assembly;
                    }
                }
            }
            catch
            {
                // If we can't get the calling assembly, don't exclude
            }

            return null;
        }

        /// <summary>
        /// Converts a dynamic object to a dictionary using reflection
        /// </summary>
        internal static Dictionary<string, object> ConvertObjectToDictionary(object obj)
        {
            var dictionary = new Dictionary<string, object>();

            if (obj == null) return dictionary;

            // Handle if it's already an IDictionary
            if (obj is IDictionary<string, object> existingDict)
            {
                return new Dictionary<string, object>(existingDict);
            }

            // Use reflection to get all properties
            var type = obj.GetType();
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                try
                {
                    var value = property.GetValue(obj);
                    dictionary[property.Name] = value;
                }
                catch
                {
                    // Skip properties that can't be accessed
                }
            }

            return dictionary;
        }

        public static void ClearCache()
        {
            _types.Clear();
            _assemblies.Clear();
        }
    }
}
