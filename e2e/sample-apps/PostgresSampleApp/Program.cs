using Npgsql;
using System.Text.Json;
using Aikido.Zen.DotNetCore;
using PostgresSampleApp;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddZenFireWall();

// Add Postgres connection
var connectionString = builder.Configuration.GetConnectionString("PostgresConnection");
DatabaseService.ConnectionString = connectionString;

var app = builder.Build();

app.UseDeveloperExceptionPage();
app.UseZenFireWall();
app.UseHttpsRedirection();

// Pets endpoints
app.MapGet("/api/pets", async () =>
{
    var pets = DatabaseService.GetAllPets();
    return Results.Ok(pets);
});

app.MapGet("/api/pets/{id:int}", async (int id) =>
{
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

    var rowsCreated = DatabaseService.CreatePetByName(petData.Name);
    return Results.Ok(new { Rows = rowsCreated });
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
    await DatabaseService.EnsureDatabaseSetupAsync();
});

app.Run();
