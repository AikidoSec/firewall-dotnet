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
using Microsoft.AspNetCore.Http;

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

            // make sure we use the httpcontext accessor
            services.AddHttpContextAccessor();
            // now we can register our context accessor
            services.AddTransient<ContextAccessor>(factory =>
            {
                var httpContextAccessor = factory.GetRequiredService<IHttpContextAccessor>();
                return new ContextAccessor(httpContextAccessor);
            });

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
			var options = app.ApplicationServices.GetRequiredService<IOptions<AikidoOptions>>();
			if (options?.Value?.AikidoToken != null) {
                var agent = Agent.GetInstance(app.ApplicationServices.GetRequiredService<IZenApi>());
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
                options.AikidoUrl = configuration["Aikido:AikidoUrl"] ?? Environment.GetEnvironmentVariable("AIKIDO_URL");
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AIKIDO_URL"))) {
                    Environment.SetEnvironmentVariable("AIKIDO_URL", options.AikidoUrl);
                }
            });
            return services;
		}

		internal static IServiceCollection AddZenApi(this IServiceCollection services) {
			services.AddTransient<IReportingAPIClient>(provider =>
			{
				return new ReportingAPIClient();
			});
            services.AddTransient<IRuntimeAPIClient>(provider =>
            {
                return new RuntimeAPIClient();
            });
			services.AddTransient<IZenApi, ZenApi>();
			return services;
		}
	}
}
