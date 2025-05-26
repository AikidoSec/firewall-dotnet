using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.DotNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SampleApp.Common.Controllers;

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
            services.AddHttpClient();
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

                // Endpoint to trigger an outbound HTTP request for testing hostname capture
                endpoints.MapGet("/api/outboundRequest", async (HttpContext httpContext, [FromServices] IHttpClientFactory clientFactory) =>
                {
                    if (!httpContext.Request.Query.TryGetValue("uri", out var uriValues) ||
                        !Uri.TryCreate(uriValues.FirstOrDefault(), UriKind.Absolute, out var targetUri))
                    {
                        httpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        await httpContext.Response.WriteAsync("Missing or invalid 'uri' query parameter. Please provide an absolute URI (e.g., http://example.com).");
                        return;
                    }

                    var client = clientFactory.CreateClient("AikidoAgentTestClient"); // Use a named client for potential future config

                    // Use a CancellationToken with a short timeout
                    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

                    try
                    {
                        // Make the request - we don't need the response or detailed error handling
                        _ = await client.GetAsync(targetUri, cts.Token);
                    }
                    catch (Exception)
                    {
                        // Ignore exceptions (e.g., timeouts, DNS errors, connection refused)
                        // We only care that the request attempt was made for agent capture.
                    }

                    // Always return OK, the test checks the agent context, not this response body
                    await httpContext.Response.WriteAsync($"Attempted outbound request to {targetUri}");
                });

                // Endpoint to test WebRequest SSRF detection
                endpoints.MapGet("/api/webRequestTest", async (HttpContext httpContext) =>
                {
                    if (!httpContext.Request.Query.TryGetValue("uri", out var uriValues) ||
                        !Uri.TryCreate(uriValues.FirstOrDefault(), UriKind.Absolute, out var targetUri))
                    {
                        httpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        await httpContext.Response.WriteAsync("Missing or invalid 'uri' query parameter. Please provide an absolute URI (e.g., http://example.com).");
                        return;
                    }

                    try
                    {
                        var request = WebRequest.Create(targetUri);
                        using var response = await request.GetResponseAsync();
                        await httpContext.Response.WriteAsync($"WebRequest succeeded to {targetUri}");
                    }
                    catch (Exception ex)
                    {
                        if (ex is AikidoException)
                        {
                            throw; // Let the global exception handler deal with AikidoException
                        }
                        // For other exceptions, return a 500 error
                        httpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                        await httpContext.Response.WriteAsync($"WebRequest failed: {ex.Message}");
                    }
                });

                // Endpoint to test HttpClient SSRF detection
                endpoints.MapGet("/api/httpClientTest", async (HttpContext httpContext, [FromServices] IHttpClientFactory clientFactory) =>
                {
                    if (!httpContext.Request.Query.TryGetValue("uri", out var uriValues) ||
                        !Uri.TryCreate(uriValues.FirstOrDefault(), UriKind.Absolute, out var targetUri))
                    {
                        httpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        await httpContext.Response.WriteAsync("Missing or invalid 'uri' query parameter. Please provide an absolute URI (e.g., http://example.com).");
                        return;
                    }

                    try
                    {
                        var client = clientFactory.CreateClient("AikidoAgentTestClient");
                        using var response = await client.GetAsync(targetUri);
                        await httpContext.Response.WriteAsync($"HttpClient succeeded to {targetUri}");
                    }
                    catch (Exception ex)
                    {
                        if (ex is AikidoException)
                        {
                            throw; // Let the global exception handler deal with AikidoException
                        }
                        // For other exceptions, return a 500 error
                        httpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                        await httpContext.Response.WriteAsync($"HttpClient failed: {ex.Message}");
                    }
                });

                // Stats endpoint
                endpoints.MapGet("/api/getStats", () =>
                {
                    Thread.Sleep(100); // make sure the stats are updated
                    var context = Agent.Instance.Context;
                    if (context == null)
                    {
                        return Results.NotFound("Agent context not available.");
                    }
                    // Create an anonymous object with the requested stats
                    var stats = new
                    {
                        Domains = string.Join(",", context.Hostnames.Select(h => $"{h.Hostname}:{h.Port}")), // Extract just the hostname strings
                        Requests = context.Requests.ToString(),
                        AttacksDetected = context.AttacksDetected.ToString(),
                        AttacksBlocked = context.AttacksBlocked.ToString(),
                        RequestsAborted = context.RequestsAborted.ToString()
                    };

                    return Results.Ok(stats); // Return HTTP 200 OK with the stats object
                });

                // generic api endpoint /api/v1/{slug} using mapget
                endpoints.MapGet("/api/v1/{slug}", (string slug) =>
                {
                    return Results.Ok(new { slug = slug });
                });

                // generic api endpoint /api/v1/{slug}/test using mapget
                endpoints.MapGet("/api/v1/{slug}/test", (string slug) =>
                {
                    return Results.Ok(new { slug = slug });
                });

                endpoints.MapGet("/api/prioritytest/{id}", (int id) =>
                {
                    return Results.Ok(new { id = id });
                });
                endpoints.MapPost("/api/prioritytest/{id}", (int id) =>
                {
                    return Results.Ok(new { id = id });
                });
            });
        }
    }
}
