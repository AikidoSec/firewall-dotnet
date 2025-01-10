using Microsoft.Data.SqlClient;
using System.Text.Json;
using Aikido.Zen.DotNetCore;
using SqlServerSampleApp; // Assuming DatabaseService is in this namespace

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddZenFireWall();

// Add SQL Server connection
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("SQL Server connection string not found in configuration");
}
DatabaseService.ConnectionString = connectionString;
builder.Logging.AddConsole();

var app = builder.Build();

app.Logger.LogInformation("Starting application");

// Then other middleware
app.UseDeveloperExceptionPage();
app.UseZenFireWall();

// log the connection string
app.Logger.LogInformation($"Connection string: {connectionString}");

// Pets endpoints
app.MapGet("/api/pets", async () =>
{
    // Retrieve all pets from the database
    var pets = DatabaseService.GetAllPets();
    return Results.Ok(pets);
});

app.MapGet("/api/pets/{id:int}", async (int id) =>
{
    // Retrieve a pet by its ID
    var pet = DatabaseService.GetPetById(id);
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

    // Create a new pet in the database
    var rowsCreated = DatabaseService.CreatePetByName(petData.Name);
    return Results.Ok(new { Rows = rowsCreated, Blocking = Environment.GetEnvironmentVariable("AIKIDO_BLOCKING"), Name = petData.Name });
});

// Health endpoint
app.MapGet("/health", async () =>
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

app.Lifetime.ApplicationStarted.Register(async () =>
{
    try
    {
        app.Logger.LogInformation("Initializing database connection...");
        await DatabaseService.EnsureDatabaseSetupAsync();
        app.Logger.LogInformation("Database connection established successfully");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Failed to initialize database connection");
        app.Logger.LogError($"Connection string: {connectionString}");
        // Optionally terminate the application
        Environment.Exit(1);
    }
});

app.Run();
