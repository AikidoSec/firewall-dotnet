using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// Helper class for type and method reflection operations.
    /// </summary>
    internal static class TypeReflectionHelper
    {
        private static readonly ConcurrentDictionary<string, Type> _types = new ConcurrentDictionary<string, Type>();
        private static readonly ConcurrentDictionary<Type, List<Type>> _genericImplementationsCache = new ConcurrentDictionary<Type, List<Type>>();
        private static readonly ConcurrentDictionary<(Type, string), IEnumerable<MethodInfo>> _methodCache = new ConcurrentDictionary<(Type, string), IEnumerable<MethodInfo>>();

        /// <summary>
        /// Gets a type from an assembly, using caching for better performance.
        /// </summary>
        internal static Type GetTypeFromAssembly(Assembly assembly, string typeName)
        {
            var typeKey = $"{assembly.GetName().Name}.{typeName}";
            return _types.GetOrAdd(typeKey, _ =>
            {
                // First try exact match with full name
                var type = assembly.ExportedTypes.FirstOrDefault(t => t.FullName == typeName);
                if (type != null) return type;

                // If not found, try with assembly qualified name
                type = assembly.ExportedTypes.FirstOrDefault(t => t.AssemblyQualifiedName == typeName);
                if (type != null) return type;

                // Finally, try with just the type name as fallback
                return assembly.ExportedTypes.FirstOrDefault(t => t.Name == typeName);
            });
        }

        /// <summary>
        /// Gets all methods from a type by name, including generic methods.
        /// </summary>
        /// <param name="type">The type to get methods from</param>
        /// <param name="methodName">The name of the method to find</param>
        /// <param name="parameterTypeNames">Optional parameter type names to match against</param>
        /// <returns>Collection of matching method infos</returns>
        internal static IEnumerable<MethodInfo> GetMethodsFromType(Type type, string methodName, params string[] parameterTypeNames)
        {
            var cacheKey = (type, methodName);
            return _methodCache.GetOrAdd(cacheKey, _ =>
            {
                var methods = type.GetMethods(
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Instance |
                    BindingFlags.Static
                );

                var matchingMethods = new List<MethodInfo>();

                foreach (var method in methods)
                {
                    if (method.Name != methodName) continue;
                    if (parameterTypeNames.Length == 0)
                    {
                        matchingMethods.Add(method);
                        continue;
                    }

                    var parameters = method.GetParameters();
                    if (parameters.Length != parameterTypeNames.Length) continue;

                    bool isMatch = true;
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        var paramType = parameters[i].ParameterType;
                        var expectedTypeName = parameterTypeNames[i];

                        // Handle generic parameter types
                        if (paramType.IsGenericType)
                        {
                            var genericTypeDef = paramType.GetGenericTypeDefinition();
                            var implementations = GetGenericTypeImplementations(genericTypeDef);

                            // Check if any implementation matches the expected type name using full names
                            bool hasMatchingImplementation = implementations.Any(impl =>
                                impl.FullName == expectedTypeName ||
                                impl.AssemblyQualifiedName == expectedTypeName);

                            if (!hasMatchingImplementation)
                            {
                                isMatch = false;
                                break;
                            }
                        }
                        else
                        {
                            // For non-generic types, compare full type names
                            var actualTypeName = paramType.IsGenericParameter
                                ? paramType.Name  // For generic parameters, we still use just the name
                                : paramType.FullName ?? paramType.AssemblyQualifiedName;

                            if (actualTypeName != expectedTypeName)
                            {
                                isMatch = false;
                                break;
                            }
                        }
                    }

                    if (isMatch)
                    {
                        matchingMethods.Add(method);
                    }
                }

                return matchingMethods;
            });
        }

        /// <summary>
        /// Gets all concrete implementations of a generic type definition from the specified assemblies.
        /// </summary>
        internal static List<Type> GetGenericTypeImplementations(Type genericTypeDefinition)
        {
            if (!genericTypeDefinition.IsGenericTypeDefinition)
            {
                throw new ArgumentException("Type must be a generic type definition", nameof(genericTypeDefinition));
            }

            return _genericImplementationsCache.GetOrAdd(genericTypeDefinition, _ =>
            {
                var implementations = new List<Type>();

                foreach (var assembly in AssemblyHelper.GetAssemblies())
                {
                    var fullName = assembly.FullName;
                    try
                    {
                        var types = assembly.GetTypes()
                            .Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericTypeDefinition)
                            .Where(t =>
                            {
                                try
                                {
                                    if (t.IsGenericType && t.GetGenericTypeDefinition() == genericTypeDefinition)
                                        return true;

                                    return t.GetInterfaces()
                                        .Where(i => i.IsGenericType && !i.IsGenericTypeDefinition)
                                        .Any(i => i.GetGenericTypeDefinition() == genericTypeDefinition);
                                }
                                catch (TypeLoadException)
                                {
                                    return false;
                                }
                            })
                            .ToList();

                        implementations.AddRange(types);

                        // Cache the types we found using full names
                        foreach (var type in types)
                        {
                            _types.TryAdd($"{assembly.GetName().Name}.{type.FullName}", type);
                        }
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        // Try to get any types that were successfully loaded
                        var loadedTypes = ex.Types?.Where(t => t != null);
                        if (loadedTypes != null)
                        {
                            var validTypes = loadedTypes
                                .Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericTypeDefinition)
                                .Where(t => t.GetInterfaces()
                                    .Where(i => i.IsGenericType && !i.IsGenericTypeDefinition)
                                    .Any(i => i.GetGenericTypeDefinition() == genericTypeDefinition))
                                .ToList();

                            implementations.AddRange(validTypes);
                        }
                    }
                    catch (Exception)
                    {
                        // Skip other exceptions but consider logging them
                        continue;
                    }
                }

                return implementations;
            });
        }

        /// <summary>
        /// Gets all implementing classes for a given interface type from the specified assemblies.
        /// Automatically handles both generic and non-generic types.
        /// </summary>
        /// <param name="interfaceType">The interface type to find implementations for</param>
        /// <param name="assemblies">The assemblies to search in</param>
        /// <returns>List of implementing types</returns>
        internal static List<Type> GetImplementingClasses(Type interfaceType, IEnumerable<Assembly> assemblies)
        {
            // If it's a generic interface definition, use GetGenericTypeImplementations
            if (interfaceType.IsGenericTypeDefinition)
            {
                return GetGenericTypeImplementations(interfaceType);
            }

            var implementingTypes = new List<Type>();

            foreach (var assembly in assemblies)
            {
                try
                {
                    var types = assembly.DefinedTypes
                        .Where(t => t.IsClass && !t.IsAbstract)
                        .Where(t => t.ImplementedInterfaces.Any(ii =>
                        {
                            // Check if the interface is generic
                            if (ii.IsGenericType && interfaceType.IsGenericType)
                            {
                                return ii.GetGenericTypeDefinition() == interfaceType.GetGenericTypeDefinition();
                            }
                            return ii == interfaceType;
                        }));
                    implementingTypes.AddRange(types);

                    // Cache the types we found using full names
                    foreach (var type in types)
                    {
                        _types.TryAdd($"{assembly.GetName().Name}.{type.FullName}", type);
                    }
                }
                catch (Exception)
                {
                    // Skip problematic assemblies
                    continue;
                }
            }

            return implementingTypes;
        }

        /// <summary>
        /// Clears all type-related caches.
        /// </summary>
        internal static void ClearCache()
        {
            _types.Clear();
            _genericImplementationsCache.Clear();
            _methodCache.Clear();
        }
    }
}
