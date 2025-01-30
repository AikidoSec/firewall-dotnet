using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Models.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

[assembly: InternalsVisibleTo("Aikido.Zen.Tests")]
namespace Aikido.Zen.Core
{
    /// <summary>
    /// Manages event processing, scheduling and reporting for the Aikido Zen monitoring system.
    /// Handles rate limiting, retries, and graceful shutdown of event processing.
    /// </summary>
    public class Agent : IDisposable
    {
        private readonly IZenApi _api;
        private readonly ConcurrentQueue<QueuedItem> _eventQueue;
        private readonly CancellationTokenSource _cancellationSource;
        private readonly Task _backgroundTask;
        private readonly int _batchTimeoutMs;
        private readonly ConcurrentDictionary<string, ScheduledItem> _scheduledEvents;
        private long _lastConfigCheck = DateTime.UtcNow.Ticks;
        private static ILogger _logger = NullLogger.Instance;

        // Rate limiting and timing constants for the event processing loop
        private const int RateLimitPerSecond = 10;

        // this is internal, so we can change it in our unit tests
        internal const int RetryDelayMs = 250;
        private const int EmptyQueueDelayMs = 100;
        private const int ErrorRetryDelayMs = 1000;

        private AgentContext _context;

        // instance
        private static Agent _instance;

        /// <summary>
        /// Configures a static logger for Agent.
        /// If not configured, uses NullLogger which safely does nothing.
        /// </summary>
        /// <param name="logger">The logger instance to use</param>
        public static void ConfigureLogger(ILogger logger)
        {
            _logger = logger ?? NullLogger.Instance;
        }

        public static Agent Instance
        {
            get
            {
                if (_instance == null)
                    _instance = NewInstance(new ZenApi(new ReportingAPIClient(), new RuntimeAPIClient()));
                return _instance;
            }
        }

        public static Agent NewInstance(IZenApi api, int batchTimeoutMs = 5000)
        {
            _instance = new Agent(api, batchTimeoutMs);
            return _instance;
        }

        /// <summary>
        /// Initializes a new instance of the Agent class.
        /// </summary>
        /// <param name="api">The Zen API client for reporting events</param>
        /// <param name="batchTimeoutMs">Timeout in milliseconds for batch operations</param>
        public Agent(IZenApi api, int batchTimeoutMs = 5000)
        {
            // batchTimeout should be at least 1 second
            if (batchTimeoutMs < 1000)
            {
                throw new ArgumentException("Batch timeout must be at least 1000 ms", nameof(batchTimeoutMs));
            }

            _api = api ?? throw new ArgumentNullException(nameof(api));
            _eventQueue = new ConcurrentQueue<QueuedItem>();
            _scheduledEvents = new ConcurrentDictionary<string, ScheduledItem>();
            _cancellationSource = new CancellationTokenSource();
            _batchTimeoutMs = batchTimeoutMs;
            _backgroundTask = Task.Run(ProcessRecurringTasksAsync);
            _context = new AgentContext();

            // Add a console logger if in debug mode
            if (EnvironmentHelper.IsDebugging)
            {
                var loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.AddConsole();
                });
                //add the current logger to the factory
                loggerFactory.AddProvider(new LoggerProvider(logger));
                _logger = loggerFactory.CreateLogger<Agent>();
            }
        }

        /// <summary>
        /// Starts the agent and schedules heartbeat events.
        /// </summary>
        /// <param name="token">The authentication token for the Zen API</param>
        public void Start()
        {
            // send started event
            QueueEvent(EnvironmentHelper.Token, Started.Create(),
            (evt, response) =>
            {
                if (response.Success)
                {
                    var reportingResponse = response as ReportingAPIResponse;
                    Task.Run(() => UpdateConfig(reportingResponse.Block, reportingResponse.BlockedUserIds, reportingResponse.Endpoints, reportingResponse.BlockedUserAgentsRegex, reportingResponse.ConfigUpdatedAt));
                }
            });

            // get blocked ip list and add to context
            Task.Run(UpdateBlockedIps);

            // Schedule heartbeat event every x minutes
            var scheduledHeartBeat = new ScheduledItem
            {
                Token = EnvironmentHelper.Token,
                EventFactory = ConstructHeartbeat,
                Interval = Heartbeat.Interval,
                Callback = (evt, response) =>
                {
                    var reportingResponse = response as ReportingAPIResponse;
                    if (reportingResponse != null && reportingResponse.Success)
                    {
                        _context.UpdateBlockedUsers(reportingResponse.BlockedUserIds);
                        _context.UpdateBlockedUserAgents(reportingResponse.BlockedUserAgentsRegex);
                    }
                }
            };
            ScheduleEvent(
                Heartbeat.ScheduleId,
                scheduledHeartBeat
            );
        }

        /// <summary>
        /// Queues an event for processing and reporting to the Zen API.
        /// </summary>
        /// <param name="token">The authentication token</param>
        /// <param name="evt">The event to queue</param>
        /// <param name="callback">Optional callback to execute when the event is processed</param>
        public void QueueEvent(string token, IEvent evt, Action<IEvent, APIResponse> callback = null)
        {
            if (string.IsNullOrEmpty(token)) throw new ArgumentNullException(nameof(token));
            if (evt == null) throw new ArgumentNullException(nameof(evt));

            var queuedItem = new QueuedItem
            {
                Token = token,
                Event = evt,
                Callback = callback,
            };

            _eventQueue.Enqueue(queuedItem);
        }

        /// <summary>
        /// Schedules an event to be triggered at regular intervals.
        /// </summary>
        /// <param name="scheduleId">Unique identifier for the scheduled event</param>
        /// <param name="item">The item to schedule</param>
        public void ScheduleEvent(string scheduleId, ScheduledItem item)
        {
            if (string.IsNullOrEmpty(item.Token)) throw new ArgumentNullException($"{nameof(item)}.{nameof(item.Token)}");
            if (string.IsNullOrEmpty(scheduleId)) throw new ArgumentNullException(nameof(scheduleId));
            if (item.Interval <= TimeSpan.Zero) throw new ArgumentException("Interval must be positive", nameof(item.Interval));

            item.NextRun = DateTime.UtcNow + item.Interval;

            _scheduledEvents.AddOrUpdate(
                scheduleId,
                item,
                (_, __) => item
            );
        }

        /// <summary>
        /// Schedules an event to be triggered at regular intervals.
        /// </summary>
        /// <param name="token">The authentication token</param>
        /// <param name="evt">The event to schedule</param>
        /// <param name="interval">The interval between event triggers</param>
        /// <param name="scheduleId">Unique identifier for the scheduled event</param>
        /// <param name="callback">Optional callback to execute when the event triggers</param>
        public void ScheduleEvent(string token, IEvent evt, TimeSpan interval, string scheduleId, Action<IEvent, APIResponse> callback)
        {
            if (string.IsNullOrEmpty(token)) throw new ArgumentNullException(nameof(token));
            if (evt == null) throw new ArgumentNullException(nameof(evt));
            if (string.IsNullOrEmpty(scheduleId)) throw new ArgumentNullException(nameof(scheduleId));
            if (interval <= TimeSpan.Zero) throw new ArgumentException("Interval must be positive", nameof(interval));

            var scheduledItem = new ScheduledItem
            {
                Token = token,
                EventFactory = () => evt,
                Interval = interval,
                NextRun = DateTime.UtcNow + interval,
                Callback = callback
            };

            _scheduledEvents.AddOrUpdate(
                scheduleId,
                scheduledItem,
                (_, __) => scheduledItem
            );
        }

        /// <summary>
        /// Removes a scheduled event.
        /// </summary>
        /// <param name="scheduleId">The ID of the scheduled event to remove</param>
        public void RemoveScheduledEvent(string scheduleId)
        {
            if (string.IsNullOrEmpty(scheduleId)) throw new ArgumentNullException(nameof(scheduleId));
            _scheduledEvents.TryRemove(scheduleId, out _);
        }

        /// <summary>
        /// Disposes the agent, canceling any pending operations and waiting for graceful shutdown.
        /// </summary>
        public void Dispose()
        {
            try
            {
                // Cancel any pending operations
                _cancellationSource.Cancel();

                // Process any remaining events in the queue synchronously
                while (!_eventQueue.IsEmpty)
                {
                    if (_eventQueue.TryDequeue(out var eventItem))
                    {
                        try
                        {
                            var response = _api.Reporting.ReportAsync(eventItem.Token, eventItem.Event, _batchTimeoutMs)
                                .ConfigureAwait(false)
                                .GetAwaiter()
                                .GetResult();
                            eventItem.Callback?.Invoke(eventItem.Event, response);
                        }
                        catch (Exception ex)
                        {
                            // pass through
                            _logger.LogError(ex, "AIKIDO: Error processing event: {event}", eventItem.Event);
                        }
                    }
                }

                // Wait for background task to complete gracefully
                if (!_backgroundTask.Wait(TimeSpan.FromSeconds(30)))
                {
                    // pass through
                }
            }
            catch (Exception)
            {
                // pass through
            }
            finally
            {
                _cancellationSource.Dispose();
            }
        }

        /// <summary>
        /// Gets the current agent context containing monitoring data.
        /// </summary>
        public AgentContext Context => _context;

        /// <summary>
        /// Clears all monitoring data from the current context.
        /// </summary>
        public void ClearContext()
        {
            _context.Clear();
        }

        /// <summary>
        /// Captures inbound requests.
        /// </summary>
        /// <param name="context">The current context</param>
        public void CaptureInboundRequest(Context context)
        {
            if (context.User != null)
                _context.AddUser(context.User, context.RemoteAddress);
            _context.AddRequest();
        }

        /// <summary>
        /// Adds a route to the context
        /// </summary>
        /// <param name="context">The context of the request</param>
        public void AddRoute(Context context)
        {
            _context.AddRoute(context);
        }

        /// <summary>
        /// Captures the current user
        /// </summary>
        /// <param name="user"></param>
        /// <param name="ipAddress"></param>
        public void CaptureUser(User user, string ipAddress)
        {
            _context.AddUser(user, ipAddress);
        }

        /// <summary>
        /// Captures outbound requests.
        /// </summary>
        /// <param name="host"></param>
        /// <param name="port"></param>
        public void CaptureOutboundRequest(string host, int? port)
        {
            if (string.IsNullOrEmpty(host))
                return;
            _context.AddHostname(host + (port.HasValue ? $":{port}" : ""));
        }

        /// <summary>
        /// Sets the flag indicating whether the context middleware is installed.
        /// </summary>
        /// <param name="installed">True if the context middleware is installed; otherwise, false.</param>
        public void SetContextMiddlewareInstalled(bool installed)
        {
            _context.ContextMiddlewareInstalled = installed;
        }

        /// <summary>
        /// Sets the flag indicating whether the blocking middleware is installed.
        /// </summary>
        /// <param name="installed">True if the blocking middleware is installed; otherwise, false.</param>
        public void SetBlockingMiddlewareInstalled(bool installed)
        {
            _context.BlockingMiddlewareInstalled = installed;
        }

        /// <summary>
        /// Sends out an attack event
        /// </summary>
        /// <param name="kind">The attack kind</param>
        /// <param name="source">The source of the attack</param>
        /// <param name="payload">The attack payload</param>
        /// <param name="operation">The operation where the attack was detected</param>
        /// <param name="context">The context of the attack</param>
        /// <param name="module">The module where the attack was detected</param>
        /// <param name="metadata">Additional metadata for the attack</param>
        /// <param name="blocked">Whether the attack was blocked</param>
        /// <returns></returns>
        public virtual void SendAttackEvent(AttackKind kind, Source source, string payload, string operation, Context context, string module, IDictionary<string, object> metadata, bool blocked)
        {
            _logger.LogInformation("AIKIDO: Attack detected: {kind} {source} {payload} {operation} {context} {module} {metadata} {blocked}",
                kind, source, payload, operation, context, module, metadata, blocked);
            QueueEvent(EnvironmentHelper.Token, DetectedAttack.Create(kind, source, payload, operation, context, module, metadata, blocked));
        }


        // Main event processing loop that handles config checks, scheduled events and queued events
        private async Task ProcessRecurringTasksAsync()
        {
            var currentSecond = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var requestsThisSecond = 0;

            while (!_cancellationSource.Token.IsCancellationRequested)
            {
                try
                {
                    // check for config updates every minute
                    if (_lastConfigCheck + TimeSpan.FromMinutes(1).Ticks < DateTime.UtcNow.Ticks)
                    {
                        if (ConfigChanged(out var response))
                        {
                            await UpdateConfig(response.Block, response.BlockedUserIds, response.Endpoints, response.BlockedUserAgentsRegex, response.ConfigUpdatedAt);
                        }
                        _lastConfigCheck = DateTime.UtcNow.Ticks;
                    }

                    await ProcessScheduledEvents();
                    // we rate limit ourselves to 10 requests per second to the Zen API
                    var (shouldContinue, newCurrentSecond, newRequestCount) = await HandleRateLimit(currentSecond, requestsThisSecond);
                    currentSecond = newCurrentSecond;
                    requestsThisSecond = newRequestCount;

                    if (shouldContinue)
                    {
                        continue;
                    }

                    // process any queued events
                    requestsThisSecond = await ProcessQueuedEvent(requestsThisSecond);
                }
                catch (OperationCanceledException) when (_cancellationSource.Token.IsCancellationRequested)
                {
                    break;

                }
                catch (Exception)
                {
                    await HandleUnexpectedError();
                }
            }
        }

        private async Task ProcessScheduledEvents()
        {
            var now = DateTime.UtcNow;
            foreach (var kvp in _scheduledEvents.ToArray()) // Avoid enumeration issues
            {
                if (_cancellationSource.Token.IsCancellationRequested) break;

                var scheduledItem = kvp.Value;
                if (now >= scheduledItem.NextRun)
                {
                    QueueEvent(scheduledItem.Token, scheduledItem.EventFactory.Invoke(), scheduledItem.Callback);
                    var updatedItem = new ScheduledItem
                    {
                        Token = scheduledItem.Token,
                        EventFactory = scheduledItem.EventFactory,
                        Interval = scheduledItem.Interval,
                        NextRun = scheduledItem.NextRun.Add(scheduledItem.Interval),
                        Callback = scheduledItem.Callback
                    };
                    _scheduledEvents.TryUpdate(
                        kvp.Key,
                        updatedItem,
                        scheduledItem
                    );

                }
            }
        }

        private async Task<(bool shouldContinue, long currentSecond, int requestCount)> HandleRateLimit(long currentSecond, int requestsThisSecond)
        {
            var thisSecond = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            // reset the counter if we've moved to the next second
            if (thisSecond > currentSecond)
            {
                currentSecond = thisSecond;
                requestsThisSecond = 0;
            }

            if (requestsThisSecond >= RateLimitPerSecond)
            {
                // wait until the next second starts
                var delayMs = Math.Max(0, (int)((currentSecond + 1) * 1000 - DateTimeHelper.UTCNowUnixMilliseconds()));
                await Task.Delay(delayMs, _cancellationSource.Token);
                return (true, currentSecond, requestsThisSecond);
            }

            return (false, currentSecond, requestsThisSecond);
        }

        private async Task<int> ProcessQueuedEvent(int requestsThisSecond)
        {
            if (_eventQueue.TryDequeue(out var eventItem))
            {
                return await ProcessSingleEvent(eventItem, requestsThisSecond);
            }
            else
            {
                await Task.Delay(EmptyQueueDelayMs, _cancellationSource.Token);
                return requestsThisSecond;
            }
        }

        private async Task<int> ProcessSingleEvent(QueuedItem queuedItem, int requestsThisSecond)
        {
            try
            {
                requestsThisSecond++;
                var response = await _api.Reporting.ReportAsync(queuedItem.Token, queuedItem.Event, _batchTimeoutMs)
                    .ConfigureAwait(false);

                if (!response.Success)
                {
                    if (response.Error == "rate_limited" || response.Error == "timeout")
                    {
                        _eventQueue.Enqueue(queuedItem);
                        await Task.Delay(RetryDelayMs, _cancellationSource.Token);
                    }
                    // Other errors are dropped to avoid infinite retries
                }
                queuedItem.Callback?.Invoke(queuedItem.Event, response);
            }
            catch (OperationCanceledException) when (_cancellationSource.Token.IsCancellationRequested)
            {
                // Graceful shutdown
                _eventQueue.Enqueue(queuedItem);
                throw;
            }
            catch (Exception)
            {
                // Requeue on error and delay
                _eventQueue.Enqueue(queuedItem);
                await Task.Delay(RetryDelayMs, _cancellationSource.Token);
            }
            return requestsThisSecond;
        }

        private async Task HandleUnexpectedError()
        {
            try
            {
                await Task.Delay(ErrorRetryDelayMs, _cancellationSource.Token);
            }
            catch (OperationCanceledException) when (_cancellationSource.Token.IsCancellationRequested)
            {
                throw;
            }
        }

        internal Heartbeat ConstructHeartbeat()
        {
            var heartbeat = Heartbeat.Create(_context);
            ClearContext();
            return heartbeat;
        }

        internal bool ConfigChanged(out ReportingAPIResponse response)
        {

            response = _api.Runtime.GetConfigLastUpdated(EnvironmentHelper.Token).Result;
            if (!response.Success) return false;
            if (response.ConfigUpdatedAt != _context.ConfigLastUpdated)
            {
                response = _api.Runtime.GetConfig(EnvironmentHelper.Token).Result;
                return true;
            }
            return false;
        }

        private async Task UpdateConfig(bool block, IEnumerable<string> blockedUsers, IEnumerable<EndpointConfig> endpoints, Regex blockedUserAgents, long configVersion)
        {
            _context.UpdateConfig(block, blockedUsers, endpoints, blockedUserAgents, configVersion);
            await UpdateBlockedIps();
        }

        internal async Task UpdateBlockedIps()
        {
            if (string.IsNullOrEmpty(EnvironmentHelper.Token))
                return;
            var blockedIPsResponse = await _api.Reporting.GetBlockedIps(EnvironmentHelper.Token);
            if (blockedIPsResponse.Success && blockedIPsResponse.BlockedIPAddresses != null)
            {
                _context.UpdateBlockedIps(blockedIPsResponse.Ips);
            }
        }

        public class QueuedItem
        {
            public string Token { get; set; }
            public IEvent Event { get; set; }
            public Action<IEvent, APIResponse> Callback { get; set; }
        }

        public class ScheduledItem
        {
            public string Token { get; set; }
            public Func<IEvent> EventFactory { get; set; }
            public TimeSpan Interval { get; set; }
            public DateTime NextRun { get; set; }
            public Action<IEvent, APIResponse> Callback { get; set; }
        }

    }
}
