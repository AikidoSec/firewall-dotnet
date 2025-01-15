using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Aikido.Zen.DotNetCore;

namespace SampleApps.Common
{
    /// <summary>
    /// Base startup class that provides common configuration and setup for all sample applications
    /// </summary>
    public abstract class BaseStartup
    {
        protected ILogger Logger { get; private set; }
        protected WebApplication App { get; private set; }
        protected abstract string ConnectionString { get; }

        /// <summary>
        /// Configures the application's services
        /// </summary>
        protected virtual void ConfigureServices(WebApplicationBuilder builder)
        {
            builder.Services.AddZenFireWall();
            builder.Logging.AddConsole();

            ValidateConnectionString();
            ConfigureDatabase(builder);
        }

        /// <summary>
        /// Configures the HTTP request pipeline
        /// </summary>
        protected virtual void Configure(WebApplication app)
        {
            App = app;
            Logger = app.Logger;

            app.UseDeveloperExceptionPage();
            app.UseZenFireWall();
            app.UseHttpsRedirection();

            ConfigureEndpoints(app);
        }

        /// <summary>
        /// Configures database-specific settings
        /// </summary>
        protected abstract void ConfigureDatabase(WebApplicationBuilder builder);

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
        protected virtual void ConfigureEndpoints(WebApplication app)
        {
            // Pets endpoints
            app.MapGet("/api/pets", () =>
            {
                var pets = GetAllPets();
                return Results.Ok(pets);
            });

            app.MapGet("/api/pets/{id:int}", (int id) =>
            {
                var pet = GetPetById(id);
                return pet != null ? Results.Ok(pet) : Results.NotFound();
            });

            app.MapPost("/api/pets/create", async (HttpContext context) =>
            {
                using var reader = new StreamReader(context.Request.Body);
                var body = await reader.ReadToEndAsync();
                var petData = JsonSerializer.Deserialize<PetCreate>(body);

                if (petData == null || string.IsNullOrEmpty(petData.Name))
                {
                    return Results.BadRequest("Pet name is required, " + body);
                }

                var rowsCreated = CreatePetByName(petData.Name);
                return Results.Ok(new { Rows = rowsCreated });
            });

            // Health endpoint
            app.MapGet("/health", () =>
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
        }

        /// <summary>
        /// Builds and starts the application
        /// </summary>
        public async Task<WebApplication> BuildAndRunAsync()
        {
            var builder = WebApplication.CreateBuilder();
            ConfigureServices(builder);

            var app = builder.Build();
            Configure(app);

            app.Lifetime.ApplicationStarted.Register(async () =>
            {
                try
                {
                    app.Logger.LogInformation("Initializing database connection...");
                    await EnsureDatabaseSetupAsync();
                    app.Logger.LogInformation("Database connection established successfully");
                }
                catch (Exception ex)
                {
                    app.Logger.LogError(ex, "Failed to initialize database connection");
                    app.Logger.LogError($"Connection string: {ConnectionString}");
                    Environment.Exit(1);
                }
            });

            return app;
        }

        // Abstract database operations that must be implemented by derived classes
        protected abstract IEnumerable<object> GetAllPets();
        protected abstract object GetPetById(int id);
        protected abstract int CreatePetByName(string name);
    }

    public class PetCreate
    {
        public string Name { get; set; }
    }
}