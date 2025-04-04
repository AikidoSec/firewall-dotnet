using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Aikido.Zen.DotNetCore;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using SampleApp.Common.Controllers;
using Microsoft.AspNetCore.Http;
using System.Net;
using System.Security.Claims;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Models;

namespace SampleApp.Common
{
    /// <summary>
    /// Base startup class that provides common configuration and setup for all sample applications
    /// </summary>
    public abstract class BaseStartup
    {
        protected ILogger Logger { get; private set; }
        protected abstract string ConnectionString { get; }
        protected abstract BasePetsController PetsController { get; }

        /// <summary>
        /// Configures the application's services
        /// </summary>
        public virtual void ConfigureServices(IServiceCollection services)
        {
            services.AddZenFirewall();
        }

        /// <summary>
        /// Configures the HTTP request pipeline
        /// </summary>
        public virtual void Configure(IApplicationBuilder app)
        {
            ConfigureDatabase(app);
            ValidateConnectionString();

            // when mocking, we don't have an ip address
            app.Use((context, next) =>
            {
                context.Request.HttpContext.Connection.RemoteIpAddress ??= IPAddress.Parse("192.168.0.1");
                // we manually do some Ip setup here, because our e2e tests mock http requests and have some missing request info because of that (no ip address for example)
                // for proper forwarding setup in real environments, see https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-9.0&preserve-view=true
                if (context.Request.Headers.ContainsKey("X-Forwarded-For"))
                {
                    context.Request.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse(context.Request.Headers["X-Forwarded-For"]);
                }
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TEST_USER")))
                {
                    Zen.SetUser(Environment.GetEnvironmentVariable("TEST_USER"), Environment.GetEnvironmentVariable("TEST_USER"), context);
                }
                if (context.Request.Headers.ContainsKey("user"))
                {
                    context.User = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
                    {
                        new Claim(ClaimTypes.Name, context.Request.Headers["user"]!),
                    }));
                    Zen.SetUser(context.Request.Headers["user"].ToString(), context.Request.Headers["user"].ToString(), context);
                }
                return next();
            });

            app.UseRouting();

            app.UseDeveloperExceptionPage();
            app.UseZenFirewall();
            app.UseHttpsRedirection();

            // Global exception handler for AikidoExceptions
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
            });

            app.ApplicationServices.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.Register(async () =>
            {
                Console.WriteLine("Ensuring database setup");
                await EnsureDatabaseSetupAsync();
            });

            ConfigureEndpoints(app);
        }

        /// <summary>
        /// Configures database-specific settings
        /// </summary>
        protected abstract void ConfigureDatabase(IApplicationBuilder app);

        /// <summary>
        /// Ensures the database is properly set up
        /// </summary>
        protected abstract Task EnsureDatabaseSetupAsync();

        /// <summary>
        /// Validates the connection string
        /// </summary>
        protected virtual void ValidateConnectionString()
        {
            if (string.IsNullOrEmpty(ConnectionString))
            {
                throw new InvalidOperationException($"Connection string not found in configuration for {GetType().Name}");
            }
        }

        /// <summary>
        /// Configures the application endpoints
        /// </summary>
        protected virtual void ConfigureEndpoints(IApplicationBuilder app)
        {
            app.UseEndpoints(endpoints =>
            {
                // Configure pets endpoints
                PetsController.ConfigureEndpoints(endpoints);

                // Health endpoint
                endpoints.MapGet("/health", () =>
                {
                    try
                    {
                        var env = Environment.GetEnvironmentVariables();
                        return Results.Ok(JsonSerializer.Serialize(env));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in /health endpoint: {ex}");
                        throw;
                    }
                });
            });
        }
    }
}
