using Aikido.Zen.Server.Mock.Filters;
using Aikido.Zen.Server.Mock.Models;
using Aikido.Zen.Server.Mock.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddSingleton<AppService>();
builder.Services.AddSingleton<ConfigService>();
builder.Services.AddSingleton<EventService>();

var app = builder.Build();

// Add middleware for protected routes
app.MapGet("/api/runtime/config", async (HttpContext context, ConfigService configService) =>
{
    var appModel = context.Items["app"] as AppModel;
    return Results.Json(configService.GetConfig(appModel!.Id));
}).AddEndpointFilter<AuthFilter>();

app.MapPost("/api/runtime/config", async (HttpContext context, ConfigService configService, Dictionary<string, object> config) =>
{
    var appModel = context.Items["app"] as AppModel;
    configService.UpdateConfig(appModel!.Id, config);
    return Results.Json(new { success = true });
}).AddEndpointFilter<AuthFilter>();

app.MapGet("/api/runtime/events", async (HttpContext context, EventService eventService) =>
{
    var appModel = context.Items["app"] as AppModel;
    return Results.Json(eventService.ListEvents(appModel!.Id));
}).AddEndpointFilter<AuthFilter>();

app.MapPost("/api/runtime/events", async (HttpContext context, EventService eventService, ConfigService configService, Dictionary<string, object> eventData) =>
{
    var appModel = context.Items["app"] as AppModel;
    eventService.CaptureEvent(appModel!.Id, eventData);

    if (eventData.TryGetValue("type", out var type) && type?.ToString() == "detected_attack")
    {
        return Results.Json(new { success = true });
    }

    return Results.Json(configService.GetConfig(appModel.Id));
}).AddEndpointFilter<AuthFilter>();

app.MapGet("/api/runtime/firewall/lists", async (HttpContext context, ConfigService configService) =>
{
    var appModel = context.Items["app"] as AppModel;
    return Results.Json(new
    {
        success = true,
        serviceId = appModel!.Id,
        blockedIPAddresses = configService.GetBlockedIps(appModel.Id).Select(ip => new
        {
            source = "geoip",
            description = "geo restrictions",
            ips = new[] { ip }
        }).ToList(),
        blockedUserAgents = string.Join("\n", configService.GetBlockedUserAgents(appModel.Id))
    });
}).AddEndpointFilter<AuthFilter>();

app.MapPost("/api/runtime/firewall/lists", async (HttpContext context, ConfigService configService, Dictionary<string, object> lists) =>
{
    var appModel = context.Items["app"] as AppModel;

    if (lists.TryGetValue("blockedIPAddresses", out var ips) && ips is List<string> ipList)
    {
        configService.UpdateBlockedIps(appModel!.Id, ipList);
    }

    if (lists.TryGetValue("blockedUserAgents", out var uas) && uas is string userAgents)
    {
        configService.UpdateBlockedUserAgents(appModel!.Id, userAgents);
    }

    return Results.Json(new { success = true });
}).AddEndpointFilter<AuthFilter>();

app.MapPost("/api/runtime/apps", async (AppService appService) =>
{
    var token = appService.CreateApp();
    return Results.Json(new { token });
});

app.Run("http://localhost:3000");
