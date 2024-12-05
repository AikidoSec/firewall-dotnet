using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.DotNetCore.StartupFilters;
using Microsoft.Extensions.Options;
using Aikido.Zen.DotNetCore.Middleware;

namespace Aikido.Zen.DotNetCore
{
	public static class DependencyInjection 
	{
		public static IServiceCollection AddZenFireWall(this IServiceCollection services) {
			// register the options
			services.AddOptions();

			// get the configuration
			var configuration = services.BuildServiceProvider().GetService<IConfiguration>()!;

			// register the configuration
			services.AddAikidoZenConfiguration(configuration);

			// register the startup filter
			services.AddTransient<IStartupFilter, ZenStartupFilter>();

			// register the zen Api
			services.AddZenApi();

            // register the middleware
            services.AddAikidoZenMiddleware();

            // register the agent
			services.AddAIkidoZenAgent();

			return services;
		}

		public static IApplicationBuilder UseZenFireWall(this IApplicationBuilder app) {
			var agent = app.ApplicationServices.GetRequiredService<Agent>();
			var options = app.ApplicationServices.GetRequiredService<IOptions<AikidoOptions>>();
			if (options?.Value?.AikidoToken != null) {
				agent.Start(options.Value.AikidoToken);
			}
			return app;
		}

        internal static IServiceCollection AddAikidoZenMiddleware(this IServiceCollection services)
        {
            services.AddTransient<ContextMiddleware>();
            return services;
        }

		internal static IServiceCollection AddAikidoZenConfiguration(this IServiceCollection services, IConfiguration configuration)
		{
            services.Configure<AikidoOptions>(options =>
            {
                options.AikidoToken = configuration["Aikido:AikidoToken"] ?? Environment.GetEnvironmentVariable("AIKIDO_TOKEN");
            });
            return services;
		}

		internal static IServiceCollection AddZenApi(this IServiceCollection services) {
			services.AddTransient<IReportingAPIClient>(provider =>
			{
				return new ReportingAPIClient(new Uri("https://guard.aikido.dev"));
			});
			services.AddTransient<IZenApi, ZenApi>();
			return services;
		}

		internal static IServiceCollection AddAIkidoZenAgent(this IServiceCollection services)
		{			
			// Add ReportingAgent as a singleton
			services.AddSingleton<Agent>();
			
			return services;
		}
	}
}
