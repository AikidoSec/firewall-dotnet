using MySqlConnector;
using System.Text.Json;
using Aikido.Zen.DotNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddZenFireWall();

// Add MySQL connection
var connectionString = "Server=localhost;Database=test;User ID=root;Password=test;";
builder.Services.AddScoped<MySqlConnection>(_ => new MySqlConnection(connectionString));

var app = builder.Build();

app.UseZenFireWall();

app.UseHttpsRedirection();

// MySQL endpoints for testing
app.MapPost("/add", async (HttpContext context, MySqlConnection connection) =>
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
    var command = new MySqlCommand($"INSERT INTO Users (Name) VALUES ('{name}')", connection);
    await command.ExecuteNonQueryAsync();

    return Results.Ok();
});

app.Run();
