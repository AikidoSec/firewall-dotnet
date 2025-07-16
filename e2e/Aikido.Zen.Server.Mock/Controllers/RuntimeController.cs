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

            app.MapPost("/api/runtime/events", async (HttpContext context, Dictionary<string, object> eventData) =>
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
                var blockedIps = _configService.GetBlockedIps(appModel!.Id).ToList();
                var allowedIps = _configService.GetAllowedIps(appModel.Id).ToList();
                var bypassedIps = _configService.GetBypassedIps(appModel.Id).ToList();
                var monitoredIps = _configService.GetMonitoredIps(appModel.Id).ToList();
                var userAgentDetails = _configService.GetUserAgentDetails(appModel.Id).ToList();

                var firewallListConfig = new FirewallListConfig
                {
                    Success = true,
                    ServiceId = appModel.Id,
                    BlockedIPAddresses = blockedIps,
                    AllowedIPAddresses = allowedIps,
                    BypassedIPAddresses = bypassedIps,
                    BlockedUserAgents = _configService.GetBlockedUserAgents(appModel.Id),
                    MonitoredIPAddresses = monitoredIps,
                    MonitoredUserAgents = _configService.GetMonitoredUserAgents(appModel.Id),
                    UserAgentDetails = userAgentDetails
                };

                return Results.Json(firewallListConfig);
            }).AddEndpointFilter<AuthFilter>();

            app.MapPost("/api/runtime/firewall/lists", async (HttpContext context, FirewallListConfig lists) =>
            {
                var appModel = context.Items["app"] as AppModel;

                if (lists.BlockedIPAddresses?.Any() ?? false)
                {
                    _configService.UpdateBlockedIps(appModel!.Id, lists.BlockedIPAddresses.ToList());
                }

                if (!string.IsNullOrEmpty(lists.BlockedUserAgents))
                {
                    _configService.UpdateBlockedUserAgents(appModel!.Id, lists.BlockedUserAgents);
                }

                if (lists.AllowedIPAddresses?.Any() ?? false)
                {
                    _configService.UpdateAllowedIps(appModel!.Id, lists.AllowedIPAddresses.ToList());
                }

                if (lists.BypassedIPAddresses?.Any() ?? false)
                {
                    _configService.UpdateBypassedIps(appModel!.Id, lists.BypassedIPAddresses.ToList());
                }

                if (lists.MonitoredIPAddresses?.Any() ?? false)
                {
                    _configService.UpdateMonitoredIps(appModel!.Id, lists.MonitoredIPAddresses);
                }

                if (!string.IsNullOrEmpty(lists.MonitoredUserAgents))
                {
                    _configService.UpdateMonitoredUserAgents(appModel!.Id, lists.MonitoredUserAgents);
                }

                if (lists.UserAgentDetails?.Any() ?? false)
                {
                    _configService.UpdateUserAgentDetails(appModel!.Id, lists.UserAgentDetails);
                }

                return Results.Json(new { success = true });
            }).AddEndpointFilter<AuthFilter>();

            // Apps endpoint
            app.MapPost("/api/runtime/apps", async () =>
            {
                var token = _appService.CreateApp();
                return Results.Json(new { token });
            });

            app.MapDelete("/api/runtime/events", async (HttpContext context) =>
            {
                var appModel = context.Items["app"] as AppModel;
                _eventService.ClearEvents(appModel!.Id);
                return Results.Json(new { success = true });
            }).AddEndpointFilter<AuthFilter>();
        }
    }
}
