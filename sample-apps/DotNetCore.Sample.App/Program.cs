using Aikido.Zen.DotNetCore;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

HttpClient.DefaultProxy = new WebProxy("http://localhost:8000")
{
    BypassProxyOnLocal = false,
    UseDefaultCredentials = true
};

/// <summary>
/// Creates and configures the web application
/// </summary>
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddZenFirewall();
builder.Services.AddEndpointsApiExplorer();
builder.Services
    .AddRouting()
    .AddControllers()
    .AddXmlDataContractSerializerFormatters();
var app = builder.Build();
app
    .UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    })
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
    .UseZenFirewall()
    // add controllers
    .UseEndpoints(endpoints =>
    {
        endpoints.MapControllers();
        endpoints.MapGet("/", () => "Hello World");
    });

app.Run();
