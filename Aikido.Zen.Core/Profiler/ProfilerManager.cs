using System;
using System.Runtime.InteropServices;

namespace Aikido.Zen.Core.Profiler
{
    /// <summary>
    /// Manages the lifecycle and initialization of the native profiler.
    /// </summary>
    public class ProfilerManager
    {
        private IntPtr _profilerHandle;
        private bool _isInitialized;

        /// <summary>
        /// Gets a value indicating whether the profiler is initialized.
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Initializes the profiler with the specified binary path.
        /// </summary>
        /// <param name="profilerBinaryPath">Base path where profiler binaries are located.</param>
        /// <exception cref="InvalidOperationException">Thrown when profiler is already initialized.</exception>
        public void Initialize(string profilerBinaryPath)
        {
            if (_isInitialized)
            {
                throw new InvalidOperationException("Profiler is already initialized");
            }

            if (string.IsNullOrWhiteSpace(profilerBinaryPath))
            {
                throw new ArgumentException(nameof(profilerBinaryPath));
            }

            string profilerPath = ProfilerLoader.GetProfilerPath(profilerBinaryPath);
            _profilerHandle = ProfilerLoader.LoadProfiler(profilerPath);

            // Set the CORECLR_PROFILER environment variable to indicate profiler is active
            // https://learn.microsoft.com/en-us/dotnet/core/runtime-config/debugging-profiling
            Environment.SetEnvironmentVariable("CORECLR_ENABLE_PROFILING", "1");
            Environment.SetEnvironmentVariable("CORECLR_PROFILER", "{cf0d821e-299b-5307-a3d8-b283c03916db}");
            Environment.SetEnvironmentVariable("CORECLR_PROFILER_PATH", profilerPath);

            _isInitialized = true;
        }

        /// <summary>
        /// Shuts down the profiler and releases resources.
        /// </summary>
        public void Shutdown()
        {
            if (!_isInitialized)
            {
                return;
            }

            if (_profilerHandle != IntPtr.Zero)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    FreeLibrary(_profilerHandle);
                }
                else
                {
                    dlclose(_profilerHandle);
                }
                _profilerHandle = IntPtr.Zero;
            }
            // https://learn.microsoft.com/en-us/dotnet/core/runtime-config/debugging-profiling
            Environment.SetEnvironmentVariable("CORECLR_ENABLE_PROFILING", "0");
            Environment.SetEnvironmentVariable("CORECLR_PROFILER", null);
            Environment.SetEnvironmentVariable("CORECLR_PROFILER_PATH", null);

            _isInitialized = false;
        }

        // Native methods for unloading dynamic libraries
        [DllImport("kernel32", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("libdl")]
        private static extern int dlclose(IntPtr handle);
    }
}
