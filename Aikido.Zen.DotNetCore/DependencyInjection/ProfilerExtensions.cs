using System;
using System.IO;
using Aikido.Zen.Core.Profiler;
using Microsoft.Extensions.DependencyInjection;

namespace Aikido.Zen.DotNetCore
{
    /// <summary>
    /// Extension methods for configuring the Aikido profiler in .NET Core applications.
    /// </summary>
    public static class ProfilerExtensions
    {
        /// <summary>
        /// Adds and initializes the Aikido profiler to the service collection.
        /// </summary>
        /// <param name="services">The IServiceCollection to add the profiler to.</param>
        /// <param name="profilerBinaryPath">Optional path to the profiler binaries. If not specified, will look in the application's base directory.</param>
        /// <returns>The IServiceCollection for chaining.</returns>
        public static IServiceCollection AddAikidoProfiler(this IServiceCollection services, string profilerBinaryPath = null)
        {
            // If no path specified, use the application's base directory
            profilerBinaryPath ??= Path.Combine(AppContext.BaseDirectory, "libraries");

            var profilerManager = new ProfilerManager();
            profilerManager.Initialize(profilerBinaryPath);

            // Register the profiler manager as a singleton
            services.AddSingleton(profilerManager);

            // Register a hosted service to handle shutdown
            services.AddHostedService<ProfilerHostedService>();

            return services;
        }
    }

    /// <summary>
    /// Background service to handle graceful shutdown of the profiler.
    /// </summary>
    internal class ProfilerHostedService : Microsoft.Extensions.Hosting.IHostedService
    {
        private readonly ProfilerManager _profilerManager;

        public ProfilerHostedService(ProfilerManager profilerManager)
        {
            _profilerManager = profilerManager;
        }

        public System.Threading.Tasks.Task StartAsync(System.Threading.CancellationToken cancellationToken)
        {
            return System.Threading.Tasks.Task.CompletedTask;
        }

        public System.Threading.Tasks.Task StopAsync(System.Threading.CancellationToken cancellationToken)
        {
            _profilerManager.Shutdown();
            return System.Threading.Tasks.Task.CompletedTask;
        }
    }
}
