using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// Helper class for assembly-related operations.
    /// </summary>
    internal static class AssemblyHelper
    {
        private static readonly ConcurrentDictionary<string, Assembly> _assemblies = new ConcurrentDictionary<string, Assembly>();
        private static readonly HashSet<string> _scannedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static AssemblyHelper()
        {
            // Subscribe to assembly load events to update our cache
            AppDomain.CurrentDomain.AssemblyLoad += (sender, args) =>
            {
                var assembly = args.LoadedAssembly;
                _assemblies.TryAdd(assembly.GetName().Name, assembly);
            };

            LoadAndCacheAllAssemblies();
        }

        /// <summary>
        /// Gets assemblies by their names. If no names are provided, returns all cached assemblies.
        /// If specific names are provided, attempts to load them if not already cached.
        /// </summary>
        /// <param name="assemblyNames">Optional array of assembly names to retrieve. If null or empty, returns all cached assemblies.</param>
        /// <returns>Collection of requested assemblies.</returns>
        internal static IEnumerable<Assembly> GetAssemblies(params string[] assemblyNames)
        {
            // If no specific names provided, return all cached assemblies
            if (assemblyNames == null || assemblyNames.Length == 0)
            {
                return _assemblies.Values;
            }

            var result = new List<Assembly>();
            foreach (var name in assemblyNames)
            {
                if (_assemblies.TryGetValue(name, out var cachedAssembly))
                {
                    result.Add(cachedAssembly);
                    continue;
                }

                var assembly = LoadAssembly(name);
                if (assembly != null)
                {
                    result.Add(assembly);
                }
            }

            return result;
        }

        /// <summary>
        /// Gets a single assembly by name, checking cache first.
        /// </summary>
        internal static Assembly GetAssembly(string assemblyName)
        {
            return GetAssemblies(assemblyName).FirstOrDefault();
        }

        /// <summary>
        /// Loads and caches all currently loaded assemblies and their references.
        /// </summary>
        private static void LoadAndCacheAllAssemblies()
        {
            var processedAssemblies = new HashSet<string>();
            var assembliesToProcess = new Queue<Assembly>(AppDomain.CurrentDomain.GetAssemblies());

            while (assembliesToProcess.Count > 0)
            {
                var assembly = assembliesToProcess.Dequeue();
                var assemblyName = assembly.GetName().Name;

                if (!processedAssemblies.Add(assemblyName))
                    continue;

                _assemblies.TryAdd(assemblyName, assembly);

                // Queue referenced assemblies for processing
                foreach (var reference in assembly.GetReferencedAssemblies())
                {
                    try
                    {
                        if (!processedAssemblies.Contains(reference.Name))
                        {
                            var referencedAssembly = LoadAssembly(reference.Name);
                            if (referencedAssembly != null)
                            {
                                assembliesToProcess.Enqueue(referencedAssembly);
                            }
                        }
                    }
                    catch
                    {
                        // Skip assemblies that can't be loaded
                        continue;
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to load an assembly by name using various methods.
        /// </summary>
        private static Assembly LoadAssembly(string assemblyName)
        {
            if (_assemblies.TryGetValue(assemblyName, out var cachedAssembly))
                return cachedAssembly;

            var assembly = FindAssemblyInAppDomain(assemblyName) ??
                         LoadAssemblyFromFile(assemblyName) ??
                         LoadAssemblyFromReferencedPaths(assemblyName);

            if (assembly != null)
            {
                _assemblies.TryAdd(assemblyName, assembly);
            }

            return assembly;
        }

        /// <summary>
        /// Attempts to find an assembly in the current AppDomain.
        /// </summary>
        private static Assembly FindAssemblyInAppDomain(string assemblyName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == assemblyName);
        }

        /// <summary>
        /// Attempts to load an assembly from a file.
        /// </summary>
        private static Assembly LoadAssemblyFromFile(string assemblyName)
        {
            var assemblyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{assemblyName}.dll");
            return File.Exists(assemblyPath) ? Assembly.LoadFrom(assemblyPath) : null;
        }

        /// <summary>
        /// Attempts to load an assembly from various reference paths.
        /// </summary>
        private static Assembly LoadAssemblyFromReferencedPaths(string assemblyName)
        {
            var searchPaths = new[]
            {
                AppDomain.CurrentDomain.BaseDirectory,
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                AppContext.BaseDirectory,
                Path.Combine(AppContext.BaseDirectory, "bin"),
                Path.Combine(AppContext.BaseDirectory, "refs")
            }.Distinct();

            foreach (var basePath in searchPaths)
            {
                if (string.IsNullOrEmpty(basePath)) continue;

                var assemblyPath = Path.Combine(basePath, $"{assemblyName}.dll");
                if (File.Exists(assemblyPath) && !_scannedPaths.Contains(assemblyPath))
                {
                    try
                    {
                        _scannedPaths.Add(assemblyPath);
                        return Assembly.LoadFrom(assemblyPath);
                    }
                    catch (Exception)
                    {
                        // Log or handle specific exceptions if needed
                        continue;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Clears the assembly cache and scanned paths.
        /// </summary>
        internal static void ClearCache()
        {
            _assemblies.Clear();
            _scannedPaths.Clear();
        }

        /// <summary>
        /// Forces a refresh of the assembly cache.
        /// </summary>
        internal static void RefreshCache()
        {
            ClearCache();
            LoadAndCacheAllAssemblies();
        }
    }
}
