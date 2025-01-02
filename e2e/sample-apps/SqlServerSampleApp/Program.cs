using Microsoft.Data.SqlClient;
using System.Text.Json;
using Aikido.Zen.DotNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddZenFireWall();

// Add SQL Server connection
var connectionString = "Server=localhost,27014;Database=test;User Id=sa;Password=Strong@Password123!;TrustServerCertificate=True;";
builder.Services.AddScoped<SqlConnection>(_ => new SqlConnection(connectionString));

var app = builder.Build();

app.UseZenFireWall();

app.UseHttpsRedirection();

// SQL Server endpoints for testing
app.MapPost("/add", async (HttpContext context, SqlConnection connection) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync();
    var data = JsonSerializer.Deserialize<Dictionary<string, string>>(body);
    var name = "";
    
    if (!data?.TryGetValue("name", out name) ?? true || string.IsNullOrEmpty(name))
    {
        return Results.BadRequest("Name is required");
    }

    await connection.OpenAsync();

    // Intentionally vulnerable to SQL injection
    var command = new SqlCommand($"INSERT INTO Users (Name) VALUES ('{name}')", connection);
    await command.ExecuteNonQueryAsync();

    return Results.Ok();
});

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.MapGet("/health", () => Results.Ok());


app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
