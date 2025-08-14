using Aikido.Zen.Core;
using Aikido.Zen.Core.Helpers;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;

namespace Aikido.Zen.DotNetCore.RuntimeSca
{
    internal class RuntimeAssemblyTracker
    {
        private readonly IAgent _agent;
        private readonly AssemblyToPackageMatcher _assemblyToPackageMatcher;
        private AppDomain _currentAppDomain;

        // These fields are used to process assemblies in a background task
        private readonly Task _assemblyLoadProcessingBackgroundTask;
        private readonly BlockingCollection<Assembly> _assemblyLoadQueue = new BlockingCollection<Assembly>();

        // This timer is used to periodically scan the current AppDomain for new assemblies
        private static readonly TimeSpan _fullAssemblyScanInterval = TimeSpan.FromHours(12);
        private Timer _fullAssemblyScanTimer;

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
            _assemblyToPackageMatcher = new AssemblyToPackageMatcher(dependencyContextProvider, fileVersionInfoProvider);

            // Assemblies are being processed in a separate background task
            // to prevent blocking the application while processing assemblies
            _assemblyLoadProcessingBackgroundTask = Task.Run(ProcessAssemblyLoadQueueAsync);
        }

        internal void SubscribeToAppDomain(AppDomain appDomain)
        {
            if (_currentAppDomain != null)
            {
                return;
            }

            try
            {
                _currentAppDomain = appDomain;
                _currentAppDomain.AssemblyLoad += OnAssemblyLoad;

                // Assemblies need to be refreshed every so often because
                //  they are removed from the "Packages in use" if no report
                //  is sent after 24 hours.
                _fullAssemblyScanTimer = new Timer(
                    callback: FullAssemblyScanTimerCallback,
                    state: null,
                    dueTime: TimeSpan.Zero,
                    period: _fullAssemblyScanInterval);
            }
            catch (Exception ex)
            {
                LogHelper.ErrorLog(Agent.Logger, ex, $"Failed to subscribe to AppDomain events");
            }
        }

        private void FullAssemblyScanTimerCallback(object state)
        {
            var appDomain = _currentAppDomain;
            if (appDomain == null)
            {
                // If there is no appdomain to scan, we can return early.
                return;
            }

            try
            {
                // Check for new assemblies in the current AppDomain
                foreach (var assembly in appDomain.GetAssemblies())
                {
                    _assemblyLoadQueue.Add(assembly);
                }
            }
            catch (Exception ex)
            {
                LogHelper.ErrorLog(Agent.Logger, ex, $"Failed to scan AppDomain for assemblies");
            }
        }

        internal void AddAssembly(Assembly assembly)
        {
            if (assembly == null || string.IsNullOrEmpty(assembly.Location))
            {
                return;
            }

            var library = _assemblyToPackageMatcher.FindLibraryForAssembly(assembly);
            if (library == null)
            {
                return;
            }


            // The package type can be "package", "project", or "platform".
            // We are only interested in "package" type because it is used
            //  to check for vulnerabilities on packages.
            if (library.Type != "package")
            {
                return;
            }

            _agent.AddRuntimePackage(library.Name, library.Version);
        }

        private async Task ProcessAssemblyLoadQueueAsync()
        {
            foreach (var assembly in _assemblyLoadQueue.GetConsumingEnumerable())
            {
                try
                {
                    AddAssembly(assembly);
                }
                catch (Exception ex)
                {
                    LogHelper.ErrorLog(Agent.Logger, ex, $"Failed to process assembly {assembly.FullName}");
                }
            }
        }

        private void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            try
            {
                _assemblyLoadQueue.Add(args.LoadedAssembly);
            }
            catch (Exception ex)
            {
                LogHelper.ErrorLog(Agent.Logger, ex, $"Failed to process assembly load event for {args.LoadedAssembly?.FullName}");
            }
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
