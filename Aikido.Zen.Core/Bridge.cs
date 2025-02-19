using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Aikido.Zen.Core
{
    /// <summary>
    /// Callback delegate for method instrumentation.
    /// </summary>
    /// <param name="instance">The instance on which the method was called. Will be null for static methods.</param>
    /// <param name="arguments">Array of method arguments. Value types will be automatically boxed.</param>
    /// <remarks>
    /// This delegate is called whenever an instrumented method is entered.
    /// The callback receives both the instance (if any) and the method arguments.
    /// All value type arguments are automatically boxed to objects.
    /// The callback should be lightweight to minimize performance impact.
    /// Any exceptions in the callback will be caught and logged but won't affect the original method.
    /// </remarks>
    public delegate void MethodTraceCallback(object instance, object[] arguments);

    /// <summary>
    /// Bridge between the native CLR profiler and managed code.
    /// </summary>
    /// <remarks>
    /// This class serves as the communication channel between the native CLR profiler (written in C++)
    /// and the managed code that wants to instrument methods. It provides:
    ///
    /// 1. Method registration/unregistration for instrumentation
    /// 2. Callback handling when instrumented methods are called
    /// 3. Runtime detection (.NET Core vs .NET Framework)
    /// 4. Profiler state management
    ///
    /// The class must be in the Aikido.Zen.Core assembly as the native profiler explicitly looks for it there.
    ///
    /// Usage:
    /// ```csharp
    /// // Register a callback
    /// Bridge.RegisterCallback(
    ///     assemblyName: "YourAssembly",
    ///     methodName: "Your.Namespace.Class.Method",
    ///     callback: (instance, args) => {
    ///         Console.WriteLine($"Method called on {instance} with args: {string.Join(", ", args)}");
    ///     });
    ///
    /// // Later, unregister when done
    /// Bridge.UnregisterCallback(
    ///     assemblyName: "YourAssembly",
    ///     methodName: "Your.Namespace.Class.Method");
    /// ```
    /// </remarks>
    public static class Bridge
    {
        /// <summary>
        /// Thread-safe dictionary storing registered callbacks for instrumented methods.
        /// Key format is "{assemblyName}!{methodName}".
        /// </summary>
        private static readonly ConcurrentDictionary<string, MethodTraceCallback> _callbacks =
            new ConcurrentDictionary<string, MethodTraceCallback>();

        /// <summary>
        /// Indicates whether we're running on .NET Core vs .NET Framework.
        /// This affects how the profiler handles certain runtime-specific features.
        /// </summary>
        private static readonly bool IsNetCore = RuntimeInformation.FrameworkDescription.StartsWith(".NET Core");

        /// <summary>
        /// Registers a callback for method instrumentation.
        /// </summary>
        /// <param name="assemblyName">The name of the assembly containing the method to instrument</param>
        /// <param name="methodName">The full name of the method (including namespace and class)</param>
        /// <param name="callback">The callback to invoke when the method is called</param>
        /// <remarks>
        /// When registering a callback:
        /// 1. The method will be instrumented using ReJIT if it's already loaded
        /// 2. Future compilations of the method will also be instrumented
        /// 3. The callback will receive both instance and arguments when the method is called
        /// 4. Value type arguments will be automatically boxed to objects
        ///
        /// Performance Considerations:
        /// - Keep callbacks lightweight as they run in the critical path
        /// - Consider unregistering callbacks when no longer needed
        /// - Avoid registering the same method multiple times
        /// </remarks>
        public static void RegisterCallback(string assemblyName, string methodName, MethodTraceCallback callback)
        {
            var key = $"{assemblyName}!{methodName}";
            _callbacks.AddOrUpdate(key, callback, (_, __) => callback);

            // Request ReJIT for this method if it's already loaded
            RequestReJIT(assemblyName, methodName);
        }

        /// <summary>
        /// Unregisters a previously registered callback.
        /// </summary>
        /// <param name="assemblyName">The name of the assembly containing the method</param>
        /// <param name="methodName">The full name of the method (including namespace and class)</param>
        /// <remarks>
        /// After unregistering:
        /// 1. The callback will no longer be invoked for new calls
        /// 2. The method will still be instrumented until the next JIT
        /// 3. Future compilations of the method won't be instrumented
        /// </remarks>
        public static void UnregisterCallback(string assemblyName, string methodName)
        {
            var key = $"{assemblyName}!{methodName}";
            _callbacks.TryRemove(key, out _);

            // Remove method from profiler's configuration
            RemoveMethodToInstrument(assemblyName, methodName);
        }

        /// <summary>
        /// Called by the native profiler when an instrumented method is entered.
        /// </summary>
        /// <param name="methodName">The full name of the method being called</param>
        /// <param name="arguments">Array containing the instance (if non-static) and arguments</param>
        /// <remarks>
        /// This method is called directly by the native profiler through injected IL.
        /// The NoInlining attribute ensures the method isn't inlined, which is crucial for proper profiling.
        ///
        /// Arguments array format:
        /// - For instance methods: [instance, arg1, arg2, ...]
        /// - For static methods: [arg1, arg2, ...]
        ///
        /// Any exceptions in the callback are caught and logged to prevent affecting the original method.
        /// </remarks>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void OnMethodEnter(string methodName, object[] arguments)
        {
            if (_callbacks.TryGetValue(methodName, out var callback))
            {
                try
                {
                    // First argument is the instance (null for static methods)
                    var instance = arguments.Length > 0 ? arguments[0] : null;

                    // Rest are the actual method arguments
                    var methodArgs = new object[arguments.Length - 1];
                    Array.Copy(arguments, 1, methodArgs, 0, methodArgs.Length);

                    callback(instance, methodArgs);
                }
                catch (Exception ex)
                {
                    // Log error but don't throw - we don't want to affect the original method
                    Console.Error.WriteLine($"Error in profiler callback: {ex}");
                }
            }
        }

        /// <summary>
        /// Checks if the profiler is enabled.
        /// </summary>
        /// <returns>True if the profiler is enabled, false otherwise.</returns>
        /// <remarks>
        /// The profiler can be disabled by setting the AIKIDO_DISABLED environment variable:
        /// - AIKIDO_DISABLED=1
        /// - AIKIDO_DISABLED=true
        /// - AIKIDO_DISABLED=TRUE
        ///
        /// When disabled:
        /// 1. The profiler won't attach to the process
        /// 2. No methods will be instrumented
        /// 3. Callbacks won't be invoked
        /// </remarks>
        public static bool IsProfilerEnabled()
        {
            var disabled = Environment.GetEnvironmentVariable("AIKIDO_DISABLED");
            return string.IsNullOrEmpty(disabled) ||
                   !(disabled == "1" || disabled.Equals("true", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets detailed runtime information for diagnostics.
        /// </summary>
        /// <returns>A string containing framework version, OS, and process architecture.</returns>
        /// <remarks>
        /// This is useful for debugging issues related to:
        /// - Framework compatibility (.NET Core vs .NET Framework)
        /// - Platform-specific behavior
        /// - Architecture mismatches (x86 vs x64)
        /// </remarks>
        public static string GetRuntimeInfo()
        {
            return $"Framework: {RuntimeInformation.FrameworkDescription}, " +
                   $"OS: {RuntimeInformation.OSDescription}, " +
                   $"Process Architecture: {RuntimeInformation.ProcessArchitecture}";
        }

        #region Native Methods

        // Windows x64
        [DllImport("libraries/Aikido.Zen.Profiler.windows.x64.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode, EntryPoint = "RequestReJIT")]
        private static extern void RequestReJIT_windows_x64(string assemblyName, string methodName);

        [DllImport("libraries/Aikido.Zen.Profiler.windows.x64.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode, EntryPoint = "RemoveMethodToInstrument")]
        private static extern void RemoveMethodToInstrument_windows_x64(string assemblyName, string methodName);

        // Windows ARM64
        [DllImport("libraries/Aikido.Zen.Profiler.windows.arm64.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode, EntryPoint = "RequestReJIT")]
        private static extern void RequestReJIT_windows_arm64(string assemblyName, string methodName);

        [DllImport("libraries/Aikido.Zen.Profiler.windows.arm64.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode, EntryPoint = "RemoveMethodToInstrument")]
        private static extern void RemoveMethodToInstrument_windows_arm64(string assemblyName, string methodName);

        // macOS x64
        [DllImport("libraries/libAikido.Zen.Profiler.osx.x64.dylib", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, EntryPoint = "RequestReJIT")]
        private static extern void RequestReJIT_osx_x64(string assemblyName, string methodName);

        [DllImport("libraries/libAikido.Zen.Profiler.osx.x64.dylib", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, EntryPoint = "RemoveMethodToInstrument")]
        private static extern void RemoveMethodToInstrument_osx_x64(string assemblyName, string methodName);

        // macOS ARM64
        [DllImport("libraries/libAikido.Zen.Profiler.osx.arm64.dylib", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, EntryPoint = "RequestReJIT")]
        private static extern void RequestReJIT_osx_arm64(string assemblyName, string methodName);

        [DllImport("libraries/libAikido.Zen.Profiler.osx.arm64.dylib", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, EntryPoint = "RemoveMethodToInstrument")]
        private static extern void RemoveMethodToInstrument_osx_arm64(string assemblyName, string methodName);

        // Linux x64
        [DllImport("libraries/libAikido.Zen.Profiler.linux.x64.so", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, EntryPoint = "RequestReJIT")]
        private static extern void RequestReJIT_linux_x64(string assemblyName, string methodName);

        [DllImport("libraries/libAikido.Zen.Profiler.linux.x64.so", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, EntryPoint = "RemoveMethodToInstrument")]
        private static extern void RemoveMethodToInstrument_linux_x64(string assemblyName, string methodName);

        // Linux ARM64
        [DllImport("libraries/libAikido.Zen.Profiler.linux.arm64.so", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, EntryPoint = "RequestReJIT")]
        private static extern void RequestReJIT_linux_arm64(string assemblyName, string methodName);

        [DllImport("libraries/libAikido.Zen.Profiler.linux.arm64.so", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, EntryPoint = "RemoveMethodToInstrument")]
        private static extern void RemoveMethodToInstrument_linux_arm64(string assemblyName, string methodName);

        /// <summary>
        /// Requests the native profiler to ReJIT a method.
        /// </summary>
        /// <param name="assemblyName">The name of the assembly containing the method</param>
        /// <param name="methodName">The full name of the method to ReJIT</param>
        private static void RequestReJIT(string assemblyName, string methodName)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                {
                    RequestReJIT_windows_arm64(assemblyName, methodName);
                }
                else
                {
                    RequestReJIT_windows_x64(assemblyName, methodName);
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                {
                    RequestReJIT_osx_arm64(assemblyName, methodName);
                }
                else
                {
                    RequestReJIT_osx_x64(assemblyName, methodName);
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                {
                    RequestReJIT_linux_arm64(assemblyName, methodName);
                }
                else
                {
                    RequestReJIT_linux_x64(assemblyName, methodName);
                }
            }
            else
            {
                throw new PlatformNotSupportedException("Unsupported platform");
            }
        }

        /// <summary>
        /// Notifies the native profiler to stop instrumenting a method.
        /// </summary>
        /// <param name="assemblyName">The name of the assembly containing the method</param>
        /// <param name="methodName">The full name of the method to stop instrumenting</param>
        private static void RemoveMethodToInstrument(string assemblyName, string methodName)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                {
                    RemoveMethodToInstrument_windows_arm64(assemblyName, methodName);
                }
                else
                {
                    RemoveMethodToInstrument_windows_x64(assemblyName, methodName);
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                {
                    RemoveMethodToInstrument_osx_arm64(assemblyName, methodName);
                }
                else
                {
                    RemoveMethodToInstrument_osx_x64(assemblyName, methodName);
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                {
                    RemoveMethodToInstrument_linux_arm64(assemblyName, methodName);
                }
                else
                {
                    RemoveMethodToInstrument_linux_x64(assemblyName, methodName);
                }
            }
            else
            {
                throw new PlatformNotSupportedException("Unsupported platform");
            }
        }

        #endregion
    }
}
