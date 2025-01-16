using MongoDB.Driver;
using MongoDB.Bson;
using Aikido.Zen.DotNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddZenFireWall();
builder.Services.AddSingleton<IMongoClient>(new MongoClient("mongodb://localhost:27017"));

var app = builder.Build();

app.UseZenFireWall();

app.UseHttpsRedirection();

// MongoDB endpoints for testing
app.MapGet("/", async (HttpContext context, IMongoClient client) =>
{
    var search = context.Request.Query["search"].ToString();
    var database = client.GetDatabase("test");
    var collection = database.GetCollection<BsonDocument>("items");

    // This will be vulnerable to NoSQL injection when search[$ne]=null
    var filter = BsonDocument.Parse($"{{ title: {search} }}");
    var result = await collection.Find(filter).ToListAsync();
    
    return Results.Ok(result);
});

app.MapGet("/where", async (HttpContext context, IMongoClient client) =>
{
    var title = context.Request.Query["title"].ToString();
    var database = client.GetDatabase("test");
    var collection = database.GetCollection<BsonDocument>("items");

    // This will be vulnerable to NoSQL injection
    var filter = BsonDocument.Parse($"{{ title: '{title}' }}");
    var result = await collection.Find(filter).ToListAsync();
    
    return Results.Ok(result);
});

app.Run();

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

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
