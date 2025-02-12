using Aikido.Zen.Server.Mock.Filters;
using Aikido.Zen.Server.Mock.Models;
using Aikido.Zen.Server.Mock.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Aikido.Zen.Server.Mock.Controllers
{
    /// <summary>
    /// Controller handling runtime-related endpoints
    /// </summary>
    public class RuntimeController
    {
        private readonly ConfigService _configService;
        private readonly EventService _eventService;
        private readonly AppService _appService;

        public RuntimeController(ConfigService configService, EventService eventService, AppService appService)
        {
            _configService = configService;
            _eventService = eventService;
            _appService = appService;
        }

        public void ConfigureEndpoints(WebApplication app)
        {
            // Config endpoints
            app.MapGet("/api/runtime/config", async (HttpContext context) =>
            {
                var appModel = context.Items["app"] as AppModel;
                return Results.Json(_configService.GetConfig(appModel!.Id));
            }).AddEndpointFilter<AuthFilter>();

            app.MapPost("/api/runtime/config", async (HttpContext context, Dictionary<string, object> config) =>
            {
                var appModel = context.Items["app"] as AppModel;
                _configService.UpdateConfig(appModel!.Id, config);
                return Results.Json(new { success = true });
            }).AddEndpointFilter<AuthFilter>();

            // Events endpoints
            app.MapGet("/api/runtime/events", async (HttpContext context) =>
            {
                var appModel = context.Items["app"] as AppModel;
                return Results.Json(_eventService.ListEvents(appModel!.Id));
            }).AddEndpointFilter<AuthFilter>();

            app.MapPost("/api/runtime/events", (HttpContext context, Dictionary<string, object> eventData) =>
            {
                var appModel = context.Items["app"] as AppModel;
                _eventService.CaptureEvent(appModel!.Id, eventData);

                if (eventData.TryGetValue("type", out var type) && type?.ToString() == "detected_attack")
                {
                    return Results.Json(new { success = true });
                }

                return Results.Json(_configService.GetConfig(appModel.Id));
            }).AddEndpointFilter<AuthFilter>();

            // Firewall endpoints
            app.MapGet("/api/runtime/firewall/lists", async (HttpContext context) =>
            {
                var appModel = context.Items["app"] as AppModel;
                return Results.Json(new
                {
                    success = true,
                    serviceId = appModel!.Id,
                    blockedIPAddresses = _configService.GetBlockedIps(appModel.Id).Select(ip => new
                    {
                        source = "geoip",
                        description = "geo restrictions",
                        ips = new[] { ip }
                    }).ToList(),
                    blockedUserAgents = string.Join("\n", _configService.GetBlockedUserAgents(appModel.Id))
                });
            }).AddEndpointFilter<AuthFilter>();

            app.MapPost("/api/runtime/firewall/lists", async (HttpContext context, Dictionary<string, object> lists) =>
            {
                var appModel = context.Items["app"] as AppModel;

                if (lists.TryGetValue("blockedIPAddresses", out var ips) && ips is List<string> ipList)
                {
                    _configService.UpdateBlockedIps(appModel!.Id, ipList);
                }

                if (lists.TryGetValue("blockedUserAgents", out var uas) && uas is string userAgents)
                {
                    _configService.UpdateBlockedUserAgents(appModel!.Id, userAgents);
                }

                return Results.Json(new { success = true });
            }).AddEndpointFilter<AuthFilter>();

            // Apps endpoint
            app.MapPost("/api/runtime/apps", async () =>
            {
                var token = _appService.CreateApp();
                return Results.Json(new { token });
            });
        }
    }
}
