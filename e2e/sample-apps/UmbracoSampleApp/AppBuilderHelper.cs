using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Aikido.Zen.DotNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants.Permissions;
using UmbracoSampleApp.Controllers;
using Aikido.Zen.Core.Exceptions;
using System.Net;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace UmbracoSampleApp
{
    /// <summary>
    /// Helper class to configure and create a WebApplication instance.
    /// </summary>
    public static class AppBuilderHelper
    {
        /// <summary>
        /// Configures and returns a WebApplication instance.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        /// <returns>Configured WebApplication instance.</returns>
        public static async Task<WebApplication> CreateApp(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.CreateUmbracoBuilder()
                .AddBackOffice()
                .AddWebsite()
                .AddDeliveryApi()
                .AddComposers()
                .Build();
            builder.Services.AddZenFirewall();
            builder.Services.AddScoped<DatabaseService>();

            var app = builder.Build();

            await app.BootUmbracoAsync();
            DatabaseService.ConnectionString = app.Configuration.GetConnectionString("umbracoDbDSN");

            app.UseZenFirewall();

            app.UseUmbraco()
                .WithMiddleware(u =>
                {
                    u.UseBackOffice();
                    u.UseWebsite();
                    app.Use(async (context, next) =>
                    {
                        try
                        {
                            await next();
                        }
                        catch (AikidoException ex)
                        {
                            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                            await context.Response.WriteAsync("Request blocked due to security policy.");
                        }
                        catch (Exception e)
                        {
                            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                            await context.Response.WriteAsync("An error occurred.");
                        }
                    });
                })
                .WithEndpoints(u =>
                {
                    u.UseBackOfficeEndpoints();
                    u.UseWebsiteEndpoints();
                    var petsController = new PetsController(app.Services);
                    petsController.ConfigureEndpoints(u.EndpointRouteBuilder);
                });

            return app;
        }
    }

    public class UmbracoApplicationNotificationComposer : IComposer
    {
        public void Compose(IUmbracoBuilder builder)
        {
            builder.AddNotificationHandler<UmbracoApplicationStartedNotification, UmbracoApplicationNotificationHandler>();
        }
    }

    public class UmbracoApplicationNotificationHandler : INotificationHandler<UmbracoApplicationStartedNotification>
    {
        private readonly DatabaseService databaseService;
        public UmbracoApplicationNotificationHandler (DatabaseService databaseService)
        {
            this.databaseService = databaseService;
        }
        public void Handle(UmbracoApplicationStartedNotification notification)
        {
            databaseService.EnsureDatabaseSetupAsync();
        }
    }
}
