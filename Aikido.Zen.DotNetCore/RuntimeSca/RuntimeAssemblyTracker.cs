using Aikido.Zen.Core;
using Aikido.Zen.Core.Helpers;
using Microsoft.Extensions.DependencyModel;
using System.Diagnostics;
using System.Reflection;

namespace Aikido.Zen.DotNetCore.RuntimeSca
{
    internal class RuntimeAssemblyTracker
    {
        private readonly IAgent _agent;
        private readonly IDependencyContextProvider _dependencyContextProvider;
        private readonly IFileVersionInfoProvider _fileVersionInfoProvider;

        internal static RuntimeAssemblyTracker Instance { get; } = new RuntimeAssemblyTracker(
            Agent.Instance,
            new DependencyContextProvider(),
            new FileVersionInfoProvider());

        // This constructor is meant for testing purposes only.
        // Use the Instance property to access the singleton instance in production code.
        internal RuntimeAssemblyTracker(
            IAgent agent,
            IDependencyContextProvider dependencyContextProvider,
            IFileVersionInfoProvider fileVersionInfoProvider)
        {
            _agent = agent;
            _dependencyContextProvider = dependencyContextProvider;
            _fileVersionInfoProvider = fileVersionInfoProvider;
        }

        internal void SubscribeToAppDomain(AppDomain appDomain)
        {
            appDomain.AssemblyLoad += OnAssemblyLoad;
            
            // Process already loaded assemblies
            foreach (var assembly in appDomain.GetAssemblies())
            {
                AddAssembly(assembly);
            }
        }

        internal void AddAssembly(Assembly assembly)
        {
            if (assembly == null || string.IsNullOrEmpty(assembly.Location))
            {
                return;
            }

            var library = FindLibraryForAssembly(assembly);
            if (library == null)
            {
                return;
            }

            if (library.Type != "package")
            {
                return;
            }

            _agent.AddRuntimePackage(library.Name, library.Version);
        }

        private void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            try
            {
                AddAssembly(args.LoadedAssembly);
            }
            catch (Exception ex)
            {
                LogHelper.ErrorLog(Agent.Logger, ex, $"Failed to process assembly load event for {args.LoadedAssembly?.FullName}");
            }
        }

        private RuntimeLibrary FindLibraryForAssembly(Assembly assembly)
        {
            if (string.IsNullOrEmpty(assembly?.Location))
            {
                return null;
            }

            var assemblyFileName = Path.GetFileName(assembly.Location);
            var assemblyVersion = assembly.GetName()?.Version?.ToString();
            
            FileVersionInfo fileVersionInfo;
            try
            {
                fileVersionInfo = _fileVersionInfoProvider.GetVersionInfo(assembly.Location);
            }
            catch (Exception)
            {
                // If we can't get file version info, we can't match the library
                return null;
            }

            // Getting the fileVersionInfo.FileVersion omits the ending '.0' in some cases.
            // This is part of the {application}.deps.json file though.
            // So we need it to match the correct assembly with the correct package.
            var fileVersion = $"{fileVersionInfo.FileMajorPart}.{fileVersionInfo.FileMinorPart}.{fileVersionInfo.FileBuildPart}.{fileVersionInfo.FilePrivatePart}";

            foreach (var library in _dependencyContextProvider.GetRuntimeLibraries())
            {
                foreach (var group in library.RuntimeAssemblyGroups)
                {
                    foreach (var file in group.RuntimeFiles)
                    {
                        if (!Path.GetFileName(file.Path).Equals(assemblyFileName, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (!assemblyVersion.Equals(file.AssemblyVersion, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (fileVersion != null &&
                            !fileVersion.Equals(file.FileVersion, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        return library;
                    }
                }
            }

            return null;
        }


        // The reason DependencyContextProvider and FileVersionInfoProvider is to allow mocking in tests.
        // The classes are marked private to prevent them from being used outside of this class.
        private class DependencyContextProvider : IDependencyContextProvider
        {
            public IEnumerable<RuntimeLibrary> GetRuntimeLibraries() => DependencyContext.Default?.RuntimeLibraries ?? Enumerable.Empty<RuntimeLibrary>();
        }
        private class FileVersionInfoProvider : IFileVersionInfoProvider
        {
            public FileVersionInfo GetVersionInfo(string fileName) => FileVersionInfo.GetVersionInfo(fileName);
        }
    }
}
