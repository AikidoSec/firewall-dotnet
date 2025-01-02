using Microsoft.Data.SqlClient;
using System.Text.Json;
using Aikido.Zen.DotNetCore;
using SqlServerSampleApp; // Assuming DatabaseService is in this namespace

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddZenFireWall();

// Add SQL Server connection
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
DatabaseService.ConnectionString = connectionString;
builder.Services.AddZenFireWall();

var app = builder.Build();

// Then other middleware
app.UseDeveloperExceptionPage();
app.UseZenFireWall();
app.UseHttpsRedirection();

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
