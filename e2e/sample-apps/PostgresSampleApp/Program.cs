using Npgsql;
using System.Text.Json;
using Aikido.Zen.DotNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddZenFireWall();

// Add Postgres connection
var connectionString = "Host=localhost;Database=test;Username=postgres;Password=test;";
builder.Services.AddScoped<NpgsqlConnection>(_ => new NpgsqlConnection(connectionString));

var app = builder.Build();

app.UseZenFireWall();

app.UseHttpsRedirection();

// Postgres endpoints for testing
app.MapPost("/add", async (HttpContext context, NpgsqlConnection connection) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync();
    var data = JsonSerializer.Deserialize<Dictionary<string, string>>(body);
    
    if (!data?.TryGetValue("name", out var name) ?? true)
    {
        return Results.BadRequest("Name is required");
    }

    await connection.OpenAsync();

    // Intentionally vulnerable to SQL injection
    var command = new NpgsqlCommand($"INSERT INTO Users (Name) VALUES ('{name}')", connection);
    await command.ExecuteNonQueryAsync();

    return Results.Ok();
});

app.Run();
