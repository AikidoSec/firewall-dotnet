using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.DotNetCore.StartupFilters;

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

			return services;
		}

		public static IApplicationBuilder UseZenFireWall(this IApplicationBuilder app) {
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
	}
}
