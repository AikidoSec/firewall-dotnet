using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.DotNetCore.StartupFilters;
using Microsoft.Extensions.Options;
using Aikido.Zen.DotNetCore.Middleware;
using Aikido.Zen.DotNetCore.Patches;

namespace Aikido.Zen.DotNetCore
{
	public static class DependencyInjection 
	{
		public static IServiceCollection AddZenFireWall(this IServiceCollection services) {
            if (Environment.GetEnvironmentVariable("AIKIDO_DISABLE") == "true")
            {
                return services;
            }

            // register the options
            services.AddOptions();

			// get the configuration
			var configuration = services.BuildServiceProvider().GetService<IConfiguration>()!;

			// register the configuration
			services.AddAikidoZenConfiguration(configuration);

            // make sure we use the httpcontext accessor
            services.AddHttpContextAccessor();
            // now we can register our context accessor
            services.AddTransient<ContextAccessor>();

			// register the startup filter
			services.AddTransient<IStartupFilter, ZenStartupFilter>();

			// register the zen Api
			services.AddZenApi();

            // register the middleware
            services.AddAikidoZenMiddleware();

			return services;
		}

		public static IApplicationBuilder UseZenFireWall(this IApplicationBuilder app) {
            if (Environment.GetEnvironmentVariable("AIKIDO_DISABLE") == "true") {
                return app;
            }
            Zen.Initialize(app.ApplicationServices);
            var agent = Agent.GetInstance(app.ApplicationServices.GetRequiredService<IZenApi>());
			var options = app.ApplicationServices.GetRequiredService<IOptions<AikidoOptions>>();
			if (options?.Value?.AikidoToken != null) {
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AIKIDO_TOKEN")))
                {
                    Environment.SetEnvironmentVariable("AIKIDO_TOKEN", options.Value.AikidoToken);
                }
				agent.Start();
			}
			Patcher.Patch();
            app.UseMiddleware<BlockingMiddleware>();
			return app;
		}

        internal static IServiceCollection AddAikidoZenMiddleware(this IServiceCollection services)
        {
            services.AddTransient<ContextMiddleware>();
            services.AddTransient<BlockingMiddleware>();
            return services;
        }

		internal static IServiceCollection AddAikidoZenConfiguration(this IServiceCollection services, IConfiguration configuration)
		{
            services.Configure<AikidoOptions>(options =>
            {
                options.AikidoToken = configuration["Aikido:AikidoToken"] ?? Environment.GetEnvironmentVariable("AIKIDO_TOKEN");
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AIKIDO_TOKEN"))) {
                    Environment.SetEnvironmentVariable("AIKIDO_TOKEN", options.AikidoToken);
                }
            });
            return services;
		}

		internal static IServiceCollection AddZenApi(this IServiceCollection services) {
            var aikidoUrl = new Uri(Environment.GetEnvironmentVariable("AIKIDO_URL") ?? "https://guard.aikido.dev");
            var runtimeUrl = new Uri(Environment.GetEnvironmentVariable("AIKIDO_REALTIME_URL") ?? "https://runtime.aikido.dev");
			services.AddTransient<IReportingAPIClient>(provider =>
			{
				return new ReportingAPIClient(aikidoUrl);
			});
            services.AddTransient<IRuntimeAPIClient>(provider =>
            {
                return new RuntimeAPIClient(runtimeUrl, aikidoUrl);
            });
			services.AddTransient<IZenApi, ZenApi>();
			return services;
		}
	}
}
