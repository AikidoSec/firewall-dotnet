using Aikido.Zen.Server.Mock.Controllers;
using Aikido.Zen.Server.Mock.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Aikido.Zen.Server.Mock
{
    /// <summary>
    /// Startup class for the mock server
    /// </summary>
    public class MockServerStartup
    {
        private readonly RuntimeController _runtimeController;
        private readonly HealthController _healthController;

        public MockServerStartup()
        {
            // Create services
            var appService = new AppService();
            var configService = new ConfigService();
            var eventService = new EventService();

            // Create controllers
            _runtimeController = new RuntimeController(configService, eventService, appService);
            _healthController = new HealthController();
        }

        /// <summary>
        /// Configures the application's services
        /// </summary>
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddResponseCompression();
            services.AddSingleton<AppService>();
            services.AddSingleton<ConfigService>();
            services.AddSingleton<EventService>();
        }

        /// <summary>
        /// Configures the HTTP request pipeline
        /// </summary>
        public void Configure(WebApplication app)
        {
            // enable gzip compression
            app.UseResponseCompression();

            app.Use(async (context, next) =>
            {
                try
                {
                    await next(context);
                }
                catch (Exception ex)
                {

                    throw;
                }
            });

            // Configure endpoints for each controller
            _runtimeController.ConfigureEndpoints(app);
            _healthController.ConfigureEndpoints(app);
        }

        /// <summary>
        /// Builds and starts the application
        /// </summary>
        public WebApplication BuildAndRun(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            ConfigureServices(builder.Services);

            var app = builder.Build();
            Configure(app);

            return app;
        }
    }
}
