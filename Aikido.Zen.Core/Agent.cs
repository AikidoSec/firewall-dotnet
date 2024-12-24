using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Models.Events;
using NetTools;

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

        // Rate limiting and timing constants for the event processing loop
        private const int RateLimitPerSecond = 10;
        private const int RetryDelayMs = 250;
        private const int EmptyQueueDelayMs = 100;
        private const int ErrorRetryDelayMs = 1000;

        private AgentContext _context;

        // instance
        private static Agent _instance;

        public static Agent Instance => _instance;

        public static Agent GetInstance(IZenApi api, int batchTimeoutMs = 5000)
        {
            if (_instance == null)
            {
                _instance = new Agent(api, batchTimeoutMs);
            }
            return _instance;
        }

        /// <summary>
        /// Initializes a new instance of the Agent class.
        /// </summary>
        /// <param name="api">The Zen API client for reporting events</param>
        /// <param name="batchTimeoutMs">Timeout in milliseconds for batch operations</param>
        public Agent(IZenApi api, int batchTimeoutMs = 5000)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _eventQueue = new ConcurrentQueue<QueuedItem>();
            _scheduledEvents = new ConcurrentDictionary<string, ScheduledItem>();
            _cancellationSource = new CancellationTokenSource();
            _batchTimeoutMs = batchTimeoutMs;
            _backgroundTask = Task.Run(ProcessRecurringTasksAsync);
            _context = new AgentContext();
        }

        /// <summary>
        /// Starts the agent and schedules heartbeat events.
        /// </summary>
        /// <param name="token">The authentication token for the Zen API</param>
        public void Start() {
            // send started event
            QueueEvent(EnvironmentHelper.Token, Started.Create(),
            (evt, response) =>
            {
                if (response.Success)
                {
                    var reportingResponse = response as ReportingAPIResponse;
                    Task.Run(() => UpdateConfig(reportingResponse.BlockedUserIds, reportingResponse.Endpoints, reportingResponse.ConfigUpdatedAt));
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
            var queuedItem = new QueuedItem {
                Token = token,
                Event = evt,
                Callback = callback,
            };
            if (string.IsNullOrEmpty(token)) throw new ArgumentNullException(nameof(token));
            if (evt == null) throw new ArgumentNullException(nameof(evt));

            _eventQueue.Enqueue(queuedItem);
        }

        /// <summary>
        /// Schedules an event to be triggered at regular intervals.
        /// </summary>
        /// <param name="scheduleId">Unique identifier for the scheduled event</param>
        /// <param name="item">The item to schedule</param>
        public void ScheduleEvent(string scheduleId, ScheduledItem item)
        {
            if (string.IsNullOrEmpty(item.Token)) throw new ArgumentNullException(nameof(item.Token));
            if (string.IsNullOrEmpty(scheduleId)) throw new ArgumentNullException(nameof(scheduleId));
            if (item.Interval <= TimeSpan.Zero) throw new ArgumentException("Interval must be positive", nameof(item.Interval));

            item.NextRun = DateTime.UtcNow + item.Interval;

            _scheduledEvents.AddOrUpdate(
                scheduleId,
                item,
                (_,__) => item
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
                NextRun = DateTime.UtcNow + interval
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
            _cancellationSource.Cancel();
            try
            {
                // Wait for background task to complete gracefully
                if (!_backgroundTask.Wait(TimeSpan.FromSeconds(30)))
                {
                    // Log warning that not all events were processed
                }
            }
            catch (TaskCanceledException) { }
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
        public void ClearContext() {
            _context.Clear();
        }

        /// <summary>
        /// Captures inbound requests.
        /// </summary>
        /// <param name="user">The user making the request</param>
        /// <param name="path">The request path</param>
        /// <param name="method">The HTTP method</param>
        /// <param name="ipAddress">The IP address of the requester</param>
        public void CaptureInboundRequest(User user, string path, string method, string ipAddress) {
            if (user != null)
                _context.AddUser(user, ipAddress);
            _context.AddRoute(path, method);
            _context.AddRequest();
        }

        /// <summary>
        /// Captures the current user
        /// </summary>
        /// <param name="user"></param>
        /// <param name="ipAddress"></param>
        public void CaptureUser(User user, string ipAddress) {
            _context.AddUser(user, ipAddress);
        }

        /// <summary>
        /// Captures outbound requests.
        /// </summary>
        /// <param name="host"></param>
        /// <param name="port"></param>
        public void CaptureOutboundRequest(string host, int? port) {
            _context.AddHostname(host + (port.HasValue ? $":{port}" : ""));
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
                            await UpdateConfig(response.BlockedUserIds, response.Endpoints, response.ConfigUpdatedAt);
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

        private Heartbeat ConstructHeartbeat() {
            var heartbeat = Heartbeat.Create(_context);
            ClearContext();
            return heartbeat;
        }

        private bool ConfigChanged(out ReportingAPIResponse response) {

            response = _api.Runtime.GetConfigVersion(EnvironmentHelper.Token).Result;
            if (!response.Success) return false;
            if (response.ConfigUpdatedAt != _context.ConfigVersion) {
                response = _api.Runtime.GetConfig(EnvironmentHelper.Token).Result;
                return true;
            }
            return false;
        }

        private async Task UpdateConfig(IEnumerable<string> blockedUsers, IEnumerable<EndpointConfig> endpoints, long configVersion) {
        
            _context.UpdateBlockedUsers(blockedUsers);
            _context.BlockList.UpdateAllowedSubnets(endpoints.ToDictionary(e => $"{e.Method}|{e.Route}", e => e.AllowedIPAddresses.Select(ip => IPAddressRange.Parse(ip))));
            _context.ConfigVersion = configVersion; 
            await UpdateBlockedIps();
        }

        private async Task UpdateBlockedIps() {
            if (string.IsNullOrEmpty(EnvironmentHelper.Token))
                return;
            var blockedIPsResponse = await _api.Reporting.GetBlockedIps(EnvironmentHelper.Token);
            if (blockedIPsResponse.Success && blockedIPsResponse.BlockedIPAddresses != null)
            {
                var blockedIPs = blockedIPsResponse.Ips();
                var ranges = new List<IPAddressRange>();
                foreach (var ip in blockedIPs)
                {
                    if (IPAddressRange.TryParse(ip, out var range))
                    {
                        ranges.Add(range);
                    }
                }
                _context.BlockList.UpdateBlockedSubnets(ranges);
            }
        }

        public class QueuedItem {
            public string Token { get; set; }
            public IEvent Event { get; set; }
            public Action<IEvent, APIResponse> Callback { get; set; }
        }

        public class ScheduledItem  {
            public string Token { get; set; }
            public Func<IEvent> EventFactory { get; set; }
            public TimeSpan Interval { get; set; }
            public DateTime NextRun { get; set; }
            public Action<IEvent, APIResponse> Callback { get; set; }
        }

	}
}
