using Aikido.Zen.Server.Mock.Filters;
using Aikido.Zen.Server.Mock.Models;
using Aikido.Zen.Server.Mock.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Aikido.Zen.Server.Mock.Controllers
{
    public class MonitoringController
    {
        private readonly ConfigService _configService;
        private readonly EventService _eventService;

        public MonitoringController(ConfigService configService, EventService eventService)
        {
            _configService = configService;
            _eventService = eventService;
        }

        public void ConfigureEndpoints(WebApplication app)
        {
            app.MapPost("/api/monitoring/configure", async (HttpContext context, [FromBody] FirewallListConfig config) =>
            {
                var appModel = context.Items["app"] as AppModel;
                if (appModel == null) return Results.Unauthorized();

                _configService.UpdateMonitoredIps(appModel.Id, config.MonitoredIPAddresses);
                _configService.UpdateMonitoredUserAgents(appModel.Id, config.MonitoredUserAgents);
                _configService.UpdateUserAgentDetails(appModel.Id, config.UserAgentDetails);
                if (config.BlockedIPAddresses != null)
                {
                    _configService.UpdateBlockedIps(appModel.Id, config.BlockedIPAddresses.ToList());
                }

                return Results.Ok(new { success = true });
            }).AddEndpointFilter<AuthFilter>();

            app.MapGet("/api/monitoring/events", async (HttpContext context) =>
            {
                var appModel = context.Items["app"] as AppModel;
                if (appModel == null) return Results.Unauthorized();
                return Results.Ok(_eventService.GetEvents(appModel.Id));
            }).AddEndpointFilter<AuthFilter>();

            app.MapDelete("/api/monitoring/events", async (HttpContext context) =>
            {
                var appModel = context.Items["app"] as AppModel;
                if (appModel == null) return Results.Unauthorized();
                _eventService.ClearEvents(appModel.Id);
                return Results.Ok(new { success = true });
            }).AddEndpointFilter<AuthFilter>();
        }
    }
}
