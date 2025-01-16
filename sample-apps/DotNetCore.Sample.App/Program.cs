using Aikido.Zen.DotNetCore;
using SQLitePCL;


/// <summary>
/// Creates and configures the web application
/// </summary>
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddZenFireWall();
builder.Services.AddEndpointsApiExplorer();
builder.Services
    .AddRouting()
    .AddControllers()
    .AddXmlDataContractSerializerFormatters();

var app = builder.Build();
app
    // add routing
    .UseRouting()
    // authorize users
    .Use((context, next) =>
    {
        var id = context.User?.Identity?.Name ?? "test";
        var name = context.User?.Identity?.Name ?? "Anonymous";
        if (!string.IsNullOrEmpty(id))
            Zen.SetUser(id, name, context);
        return next();
    })
    // add Zen middleware
    .UseZenFireWall()
    // add controllers
    .UseEndpoints(endpoints =>
    {
        endpoints.MapControllers();
        endpoints.MapGet("/", () => "Hello World");
    });

app.Run();
