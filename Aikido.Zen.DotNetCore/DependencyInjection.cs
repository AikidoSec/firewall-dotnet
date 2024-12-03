using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.DotNetCore.StartupFilters;
using Microsoft.Extensions.Options;

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

			// register the agent
			var options = services.BuildServiceProvider().GetService<IOptions<AikidoZenConfig>>();
			services.AddAIkidoZenAgent(options?.Value?.ZenToken ?? "");

			return services;
		}

		public static IApplicationBuilder UseZenFireWall(this IApplicationBuilder app) {
			var agent = app.ApplicationServices.GetRequiredService<Agent>();
			var options = app.ApplicationServices.GetRequiredService<IOptions<AikidoZenConfig>>();
			if (options?.Value?.ZenToken != null) {
				agent.Start(options.Value.ZenToken);
			}
			return app;
		}

		internal static IServiceCollection AddAikidoZenConfiguration(this IServiceCollection services, IConfiguration configuration)
		{
			services.Configure<AikidoZenConfig>(_ => configuration.GetSection(AikidoZenConfig.SectionName));
			return services;
		}

		public static IServiceCollection AddZenApi(this IServiceCollection services) {
			services.AddTransient<IReportingAPIClient>(provider =>
			{
				return new ReportingAPIClient(new Uri("https://guard.aikido.dev"));
			});
			services.AddTransient<IZenApi, ZenApi>();
			return services;
		}

		public static IServiceCollection AddAIkidoZenAgent(this IServiceCollection services, string apiToken)
		{			
			// Add ReportingAgent as a singleton
			services.AddSingleton<Agent>();
			
			return services;
		}
	}
}
