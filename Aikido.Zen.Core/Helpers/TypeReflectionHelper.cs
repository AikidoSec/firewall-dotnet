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

        // Common value types used for generic type implementations
        private static readonly Type[] CommonValueTypes = new[]
        {
            typeof(int),
            typeof(double),
            typeof(float),
            typeof(bool),
            typeof(object)
        };

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
                ).Where(m => !m.IsAbstract);

                var matchingMethods = new List<MethodInfo>();

                foreach (var method in methods)
                {
                    var currMethodName = method.Name;
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
                            // create methods with each implementation
                            foreach (var implementation in implementations)
                            {
                                var implementedMethod = method.MakeGenericMethod(implementation);
                                matchingMethods.Add(implementedMethod);
                            }
                        }
                        else
                        {

                            if (paramType.Name != expectedTypeName)
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
        /// Gets all concrete implementations of a generic type definition.
        /// Handles both single and multiple type parameters with their constraints.
        /// </summary>
        /// <param name="genericTypeDefinition">The generic type definition to create implementations for</param>
        /// <returns>List of concrete type implementations</returns>
        internal static List<Type> GetGenericTypeImplementations(Type genericTypeDefinition)
        {
            if (!genericTypeDefinition.IsGenericTypeDefinition)
            {
                throw new ArgumentException("Type must be a generic type definition", nameof(genericTypeDefinition));
            }

            return _genericImplementationsCache.GetOrAdd(genericTypeDefinition, _ =>
            {
                var implementations = new List<Type>();
                var genericArgs = genericTypeDefinition.GetGenericArguments();
                var parameterCombinations = GetGenericParameterCombinations(genericArgs);

                foreach (var combination in parameterCombinations)
                {
                    try
                    {
                        var constructedType = genericTypeDefinition.MakeGenericType(combination.ToArray());
                        implementations.Add(constructedType);
                    }
                    catch (ArgumentException)
                    {
                        // Skip invalid combinations that don't satisfy constraints
                        continue;
                    }
                }

                return implementations;
            });
        }

        /// <summary>
        /// Gets all possible combinations of type parameters that satisfy the constraints.
        /// </summary>
        /// <param name="genericParameters">Array of generic type parameters</param>
        /// <returns>List of valid type parameter combinations</returns>
        private static List<List<Type>> GetGenericParameterCombinations(Type[] genericParameters)
        {
            var result = new List<List<Type>>();
            var currentCombination = new List<Type>();

            void GenerateCombinations(int paramIndex)
            {
                if (paramIndex == genericParameters.Length)
                {
                    result.Add(new List<Type>(currentCombination));
                    return;
                }

                var parameter = genericParameters[paramIndex];
                var validTypes = GetValidTypesForParameter(parameter);

                foreach (var type in validTypes)
                {
                    currentCombination.Add(type);
                    GenerateCombinations(paramIndex + 1);
                    currentCombination.RemoveAt(currentCombination.Count - 1);
                }
            }

            GenerateCombinations(0);
            return result;
        }

        /// <summary>
        /// Gets valid types that satisfy the constraints of a generic parameter.
        /// </summary>
        /// <param name="parameter">The generic parameter to find valid types for</param>
        /// <returns>List of valid types for the parameter</returns>
        private static List<Type> GetValidTypesForParameter(Type parameter)
        {
            var validTypes = new List<Type>();
            var constraints = parameter.GetGenericParameterConstraints();
            var attributes = parameter.GenericParameterAttributes;

            bool hasReferenceTypeConstraint = (attributes & GenericParameterAttributes.ReferenceTypeConstraint) != 0;
            bool hasValueTypeConstraint = (attributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0;

            // Handle interface constraints
            var interfaceConstraints = constraints.Where(c => c.IsInterface).ToList();
            if (interfaceConstraints.Any())
            {
                foreach (var constraint in interfaceConstraints)
                {
                    validTypes.AddRange(GetImplementingClasses(constraint, AssemblyHelper.GetAssemblies()));
                }
                return validTypes.Distinct().ToList();
            }

            // Handle class constraints
            var classConstraints = constraints.Where(c => c.IsClass).ToList();
            if (classConstraints.Any())
            {
                validTypes.Add(typeof(object));
                return validTypes;
            }

            // Handle reference/value type constraints
            if (hasReferenceTypeConstraint)
            {
                validTypes.Add(typeof(object));
            }
            else if (hasValueTypeConstraint)
            {
                validTypes.AddRange(CommonValueTypes.Where(t => t.IsValueType));
            }
            else
            {
                validTypes.AddRange(CommonValueTypes);
            }

            return validTypes;
        }

        /// <summary>
        /// Gets all concrete implementations of a generic method.
        /// </summary>
        /// <param name="method">The generic method to create implementations for</param>
        /// <returns>List of concrete method implementations</returns>
        internal static List<MethodInfo> GetGenericMethodImplementations(MethodInfo method)
        {
            if (!method.IsGenericMethod)
            {
                throw new ArgumentException("Method must be generic", nameof(method));
            }

            var implementations = new List<MethodInfo>();
            var genericArgs = method.GetGenericArguments();
            var parameterCombinations = GetGenericParameterCombinations(genericArgs);

            foreach (var combination in parameterCombinations)
            {
                try
                {
                    var constructedMethod = method.MakeGenericMethod(combination.ToArray());
                    implementations.Add(constructedMethod);
                }
                catch (ArgumentException)
                {
                    // Skip invalid combinations that don't satisfy constraints
                    continue;
                }
            }

            return implementations;
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
