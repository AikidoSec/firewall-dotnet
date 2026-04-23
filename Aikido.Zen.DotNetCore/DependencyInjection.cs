using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.DotNetCore.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aikido.Zen.DotNetCore
{
    /// <summary>
    /// Builder class for configuring Zen Firewall options
    /// </summary>
    public class ZenFirewallBuilder
    {
        private readonly IServiceCollection _services;
        private HttpClient _httpClient;

        internal ZenFirewallBuilder(IServiceCollection services)
        {
            _services = services;
        }

        /// <summary>
        /// Configures a custom HttpClient to be used by the Zen API clients, helpful for testing
        /// </summary>
        /// <param name="httpClient">The HttpClient instance to use</param>
        /// <returns>The builder instance for method chaining</returns>
        public ZenFirewallBuilder UseHttpClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
            return this;
        }

        internal void ConfigureServices()
        {
            // if we have a custom httpclient, use it
            if (_httpClient != null)
            {
                _services.AddTransient<IReportingAPIClient>(provider =>
                {
                    return new ReportingAPIClient(_httpClient);
                });
                _services.AddTransient<IRuntimeAPIClient>(provider =>
                {
                    return new RuntimeAPIClient(_httpClient);
                });
            }
            // otherwise, use the default httpclient
            else
            {
                _services.AddTransient<IReportingAPIClient>(provider =>
                {
                    return new ReportingAPIClient(ApiClientHttpClientFactory.Create());
                });
                _services.AddTransient<IRuntimeAPIClient>(provider =>
                {
                    return new RuntimeAPIClient(ApiClientHttpClientFactory.Create());
                });
            }
            _services.AddTransient<IZenApi, ZenApi>();
        }
    }

    public static class DependencyInjection
    {
        /// <summary>
        /// Adds Zen Firewall services to the service collection
        /// </summary>
        /// <param name="services">The IServiceCollection to add services to</param>
        /// <param name="configureOptions">Optional delegate to configure Zen Firewall options</param>
        /// <returns>The service collection for method chaining</returns>
        public static IServiceCollection AddZenFirewall(this IServiceCollection services, Action<ZenFirewallBuilder> configureOptions = null)
        {
            if (Environment.GetEnvironmentVariable("AIKIDO_DISABLE") == "true")
            {
                return services;
            }

            // register basic logging
            services.AddLogging(builder => builder.AddConsole());

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

            // Configure Zen API with optional custom settings
            var builder = new ZenFirewallBuilder(services);
            configureOptions?.Invoke(builder);
            builder.ConfigureServices();

            // register the middleware
            services.AddAikidoZenMiddleware();

            return services;
        }

        public static IApplicationBuilder UseZenFirewall(this IApplicationBuilder app)
        {
            if (Environment.GetEnvironmentVariable("AIKIDO_DISABLE") == "true")
            {
                return app;
            }
            if (!HasEndpointRouting(app))
            {
                throw new InvalidOperationException("UseZenFirewall must be called after routing is configured, either directly via UseRouting or indirectly if using a custom framework.");
            }

            var contextAccessor = app.ApplicationServices.GetRequiredService<IHttpContextAccessor>();
            Zen.Initialize(app.ApplicationServices, contextAccessor);
            Zen.Start();
            app.UseMiddleware<ContextMiddleware>();
            app.UseMiddleware<BlockingMiddleware>();
            return app;
        }

        private static bool HasEndpointRouting(IApplicationBuilder app)
        {
            // Based on the ASP.NET Core source for VerifyEndpointRoutingMiddlewareIsRegistered
            // https://github.com/dotnet/aspnetcore/blob/main/src/Http/Routing/src/Builder/EndpointRoutingApplicationBuilderExtensions.cs#L129

            // "__EndpointRouteBuilder" is typically set in regular ASP.NET Core apps
            // "__GlobalEndpointRouteBuilder" is typically set in minimal hosting WebApplication apps (eg. Blazor)
            return app.Properties.TryGetValue("__EndpointRouteBuilder", out _) ||
                   app.Properties.TryGetValue("__GlobalEndpointRouteBuilder", out _);
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
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AIKIDO_TOKEN")))
                {
                    Environment.SetEnvironmentVariable("AIKIDO_TOKEN", options.AikidoToken);
                }
                options.AikidoUrl = configuration["Aikido:AikidoUrl"] ?? Environment.GetEnvironmentVariable("AIKIDO_URL");
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AIKIDO_URL")))
                {
                    Environment.SetEnvironmentVariable("AIKIDO_URL", options.AikidoUrl);
                }
            });
            return services;
        }

        internal static IServiceCollection AddZenApi(this IServiceCollection services)
        {
            services.AddTransient<IReportingAPIClient>(provider =>
            {
                return new ReportingAPIClient(ApiClientHttpClientFactory.Create());
            });
            services.AddTransient<IRuntimeAPIClient>(provider =>
            {
                return new RuntimeAPIClient(ApiClientHttpClientFactory.Create());
            });
            services.AddTransient<IZenApi, ZenApi>();
            return services;
        }
    }
}
