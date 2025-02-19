using System;
using System.IO;
using Aikido.Zen.Core.Profiler;

namespace Aikido.Zen.DotNetFramework.Profiler
{
    /// <summary>
    /// Handles initialization of the Aikido profiler in .NET Framework applications.
    /// </summary>
    public static class ProfilerInitializer
    {
        private static ProfilerManager _profilerManager;
        private static readonly object _lock = new object();

        /// <summary>
        /// Initializes the Aikido profiler for the application.
        /// </summary>
        /// <param name="profilerBinaryPath">Optional path to the profiler binaries. If not specified, will look in the application's base directory.</param>
        public static void Initialize(string profilerBinaryPath = null)
        {
            lock (_lock)
            {
                if (_profilerManager?.IsInitialized == true)
                {
                    return;
                }

                // If no path specified, use the application's base directory
                profilerBinaryPath ??= Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profiler");

                _profilerManager = new ProfilerManager();
                _profilerManager.Initialize(profilerBinaryPath);

                // Register for application shutdown to cleanup
                AppDomain.CurrentDomain.ProcessExit += (s, e) => Shutdown();
                AppDomain.CurrentDomain.DomainUnload += (s, e) => Shutdown();
            }
        }

        /// <summary>
        /// Shuts down the profiler and releases resources.
        /// </summary>
        public static void Shutdown()
        {
            lock (_lock)
            {
                _profilerManager?.Shutdown();
                _profilerManager = null;
            }
        }

        /// <summary>
        /// Gets the current profiler manager instance.
        /// </summary>
        public static ProfilerManager Current
        {
            get
            {
                lock (_lock)
                {
                    if (_profilerManager == null || !_profilerManager.IsInitialized)
                    {
                        throw new InvalidOperationException("Profiler is not initialized. Call Initialize() first.");
                    }
                    return _profilerManager;
                }
            }
        }
    }
}
