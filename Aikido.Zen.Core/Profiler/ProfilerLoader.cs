using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Aikido.Zen.Core.Profiler
{
    /// <summary>
    /// Handles loading and initialization of the native profiler DLL based on the current platform and architecture.
    /// </summary>
    public class ProfilerLoader
    {
        private const string DLL_NAME_WINDOWS = "Aikido.Zen.Profiler.{0}.{1}.dll";
        private const string DLL_NAME_LINUX = "libAikido.Zen.Profiler.{0}.{1}.so";
        private const string DLL_NAME_OSX = "libAikido.Zen.Profiler.{0}.{1}.dylib";

        /// <summary>
        /// Gets the path to the appropriate profiler DLL based on the current platform and architecture.
        /// </summary>
        /// <param name="basePath">Base directory where profiler binaries are located.</param>
        /// <returns>Full path to the platform-specific profiler DLL.</returns>
        public static string GetProfilerPath(string basePath)
        {
            string architecture;
            switch (RuntimeInformation.ProcessArchitecture)
            {
                case Architecture.X64:
                    architecture = "x64";
                    break;
                case Architecture.Arm64:
                    architecture = "arm64";
                    break;
                case Architecture.X86:
                    architecture = "x86";
                    break;
                case Architecture.Arm:
                    architecture = "arm";
                    break;
                default:
                    throw new PlatformNotSupportedException($"Architecture {RuntimeInformation.ProcessArchitecture} is not supported");
            }

            string platform;
            string fileName;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                platform = "windows";
                fileName = string.Format(DLL_NAME_WINDOWS, platform, architecture);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                platform = "linux";
                fileName = string.Format(DLL_NAME_LINUX, platform, architecture);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                platform = "osx";
                fileName = string.Format(DLL_NAME_OSX, platform, architecture);
            }
            else
            {
                throw new PlatformNotSupportedException("Current OS platform is not supported");
            }

            string profilerPath = Path.Combine(basePath, fileName);

            if (!File.Exists(profilerPath))
            {
                throw new FileNotFoundException($"Profiler library not found at: {profilerPath}");
            }

            return profilerPath;
        }

        /// <summary>
        /// Loads the profiler DLL into the current process.
        /// </summary>
        /// <param name="profilerPath">Full path to the profiler DLL.</param>
        /// <returns>Handle to the loaded library.</returns>
        public static IntPtr LoadProfiler(string profilerPath)
        {
            IntPtr handle;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                handle = LoadLibrary(profilerPath);
            }
            else
            {
                handle = dlopen(profilerPath, RTLD_NOW);
            }

            if (handle == IntPtr.Zero)
            {
                throw new Exception($"Failed to load profiler from {profilerPath}");
            }

            return handle;
        }

        // Native methods for loading dynamic libraries
        [DllImport("kernel32", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("libdl")]
        private static extern IntPtr dlopen(string fileName, int flags);

        private const int RTLD_NOW = 2;
    }
}
