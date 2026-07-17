using Aikido.Zen.Server.Mock.Filters;
using Aikido.Zen.Server.Mock.Models;
using Aikido.Zen.Server.Mock.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

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

            app.MapGet("/api/runtime/stream", async Task<IResult> (HttpContext context) =>
            {
                var appModel = context.Items["app"] as AppModel;
                var cancellationToken = context.RequestAborted;
                var signalLock = new object();
                var updateSignal = CreateUpdateSignal();

                void OnConfigUpdated(int appId, long configUpdatedAt)
                {
                    if (appId != appModel!.Id)
                    {
                        return;
                    }

                    lock (signalLock)
                    {
                        updateSignal.TrySetResult(configUpdatedAt);
                    }
                }

                context.Response.ContentType = "text/event-stream";
                context.Response.Headers.CacheControl = "no-cache";
                _configService.ConfigUpdated += OnConfigUpdated;

                try
                {
                    await WriteConfigUpdate(
                        context,
                        _configService.GetConfigUpdatedAt(appModel!.Id),
                        cancellationToken);

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        TaskCompletionSource<long> currentSignal;
                        lock (signalLock)
                        {
                            currentSignal = updateSignal;
                        }

                        var pingTask = Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                        var completedTask = await Task.WhenAny(currentSignal.Task, pingTask);

                        if (completedTask == currentSignal.Task)
                        {
                            var configUpdatedAt = await currentSignal.Task;
                            lock (signalLock)
                            {
                                if (ReferenceEquals(updateSignal, currentSignal))
                                {
                                    updateSignal = CreateUpdateSignal();
                                }
                            }

                            await WriteConfigUpdate(context, configUpdatedAt, cancellationToken);
                        }
                        else
                        {
                            await pingTask;
                            await context.Response.WriteAsync(": ping\n\n", cancellationToken);
                            await context.Response.Body.FlushAsync(cancellationToken);
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // The client closed the stream.
                }
                finally
                {
                    _configService.ConfigUpdated -= OnConfigUpdated;
                }

                return Results.Empty;
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
                var acceptEncodingHeader = context.Request.Headers["Accept-Encoding"].ToString().ToLower();
                if (!acceptEncodingHeader.Contains("gzip"))
                {
                    return Results.BadRequest(new {
                        success = false,
                        error = "Accept-Encoding header must include 'gzip' for firewall lists endpoint"
                    });
                }

                var appModel = context.Items["app"] as AppModel;
                var blockedIps = _configService.GetBlockedIps(appModel!.Id).ToList();

                var allowedIps = _configService.GetAllowedIps(appModel.Id).ToList();

                var firewallListConfig = new FirewallListConfig
                {
                    Success = true,
                    ServiceId = appModel.Id,
                    BlockedIPAddresses = blockedIps,
                    AllowedIPAddresses = allowedIps,
                    BlockedUserAgents = _configService.GetBlockedUserAgents(appModel.Id)
                };

                return Results.Json(firewallListConfig);
            }).AddEndpointFilter<AuthFilter>();

            app.MapPost("/api/runtime/firewall/lists", async (HttpContext context, FirewallListConfig lists) =>
            {
                var appModel = context.Items["app"] as AppModel;

                if (lists.BlockedIPAddresses?.Any() ?? false)
                {
                    _configService.UpdateBlockedIps(appModel!.Id, lists.BlockedIPAddresses);
                }

                if (!string.IsNullOrEmpty(lists.BlockedUserAgents))
                {
                    _configService.UpdateBlockedUserAgents(appModel!.Id, lists.BlockedUserAgents);
                }

                if (lists.AllowedIPAddresses?.Any() ?? false)
                {
                    _configService.UpdateAllowedIps(appModel!.Id, lists.AllowedIPAddresses);
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

        private static TaskCompletionSource<long> CreateUpdateSignal()
        {
            return new TaskCompletionSource<long>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private static async Task WriteConfigUpdate(
            HttpContext context,
            long configUpdatedAt,
            CancellationToken cancellationToken)
        {
            var data = JsonSerializer.Serialize(new { configUpdatedAt });
            await context.Response.WriteAsync(
                $"event: config-updated\ndata: {data}\n\n",
                cancellationToken);
            await context.Response.Body.FlushAsync(cancellationToken);
        }
    }
}
