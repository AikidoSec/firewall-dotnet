using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Models.Events;

namespace Aikido.Zen.Core
{
    /// <summary>
    /// Manages event processing, scheduling and reporting for the Aikido Zen monitoring system.
    /// Handles rate limiting, retries, and graceful shutdown of event processing.
    /// </summary>
    public class Agent : IDisposable
    {
        private readonly IZenApi _api;
        private readonly ConcurrentQueue<(string token, IEvent evt, Action<IEvent> callback)> _eventQueue;
        private readonly CancellationTokenSource _cancellationSource;
        private readonly Task _backgroundTask;
        private readonly int _batchTimeoutMs;
        private readonly ConcurrentDictionary<string, (string token, Func<IEvent> evt, TimeSpan interval, DateTime nextRun, Action<IEvent> callback)> _scheduledEvents;

        // Rate limiting and timing constants for the event processing loop
        private const int RateLimitPerSecond = 10;
        private const int RetryDelayMs = 250;
        private const int EmptyQueueDelayMs = 100;
        private const int ErrorRetryDelayMs = 1000;

        private AgentContext _context;

        /// <summary>
        /// Initializes a new instance of the Agent class.
        /// </summary>
        /// <param name="api">The Zen API client for reporting events</param>
        /// <param name="batchTimeoutMs">Timeout in milliseconds for batch operations</param>
        public Agent(IZenApi api, int batchTimeoutMs = 5000)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _eventQueue = new ConcurrentQueue<(string token, IEvent evt, Action<IEvent> callback)>();
            _scheduledEvents = new ConcurrentDictionary<string, (string token, Func<IEvent> evt, TimeSpan interval, DateTime nextRun, Action<IEvent> callback)>();
            _cancellationSource = new CancellationTokenSource();
            _batchTimeoutMs = batchTimeoutMs;
            _backgroundTask = Task.Run(ProcessEventsAsync);
            _context = new AgentContext();
        }

        /// <summary>
        /// Starts the agent and schedules heartbeat events.
        /// </summary>
        /// <param name="token">The authentication token for the Zen API</param>
        public void Start(string token) {
            QueueEvent(token, new Started {
                Agent = AgentInfoHelper.GetInfo()
            });

            // Schedule heartbeat event every x minutes
            ScheduleEvent(
                token: token,
                evtFactory: ConstructHeartbeat,
                interval: Heartbeat.Interval,
                scheduleId: Heartbeat.ScheduleId
            );
        }

        /// <summary>
        /// Queues an event for processing and reporting to the Zen API.
        /// </summary>
        /// <param name="token">The authentication token</param>
        /// <param name="evt">The event to queue</param>
        /// <param name="callback">Optional callback to execute when the event is processed</param>
        public void QueueEvent(string token, IEvent evt, Action<IEvent> callback = null)
        {
            if (string.IsNullOrEmpty(token)) throw new ArgumentNullException(nameof(token));
            if (evt == null) throw new ArgumentNullException(nameof(evt));

            _eventQueue.Enqueue((token, evt, callback));
        }

        /// <summary>
        /// Schedules an event to be triggered at regular intervals.
        /// </summary>
        /// <param name="token">The authentication token</param>
        /// <param name="evtFactory">The factory that creates the event to schedule</param>
        /// <param name="interval">The interval between event triggers</param>
        /// <param name="scheduleId">Unique identifier for the scheduled event</param>
        /// <param name="callback">Optional callback to execute when the event triggers</param>
        public void ScheduleEvent(string token, Func<IEvent> evtFactory, TimeSpan interval, string scheduleId, Action<IEvent> callback = null)
        {
            if (string.IsNullOrEmpty(token)) throw new ArgumentNullException(nameof(token));
            if (string.IsNullOrEmpty(scheduleId)) throw new ArgumentNullException(nameof(scheduleId));
            if (interval <= TimeSpan.Zero) throw new ArgumentException("Interval must be positive", nameof(interval));

            _scheduledEvents.AddOrUpdate(
                scheduleId,
                (token, evtFactory, interval, DateTime.UtcNow + interval, callback),
                (_, __) => (token, evtFactory, interval, DateTime.UtcNow + interval, callback)
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
        public void ScheduleEvent(string token, IEvent evt, TimeSpan interval, string scheduleId, Action<IEvent> callback)
        {
            if (string.IsNullOrEmpty(token)) throw new ArgumentNullException(nameof(token));
            if (evt == null) throw new ArgumentNullException(nameof(evt));
            if (string.IsNullOrEmpty(scheduleId)) throw new ArgumentNullException(nameof(scheduleId));
            if (interval <= TimeSpan.Zero) throw new ArgumentException("Interval must be positive", nameof(interval));

            _scheduledEvents.AddOrUpdate(
                scheduleId,
                (token, () => evt, interval, DateTime.UtcNow + interval, callback),
                (_, __) => (token, () => evt, interval, DateTime.UtcNow + interval, callback)
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
        /// Adds request context information to the monitoring data.
        /// </summary>
        /// <param name="hostname">The hostname of the request</param>
        /// <param name="user">The user making the request</param>
        /// <param name="path">The request path</param>
        /// <param name="method">The HTTP method</param>
        /// <param name="ipAddress">The IP address of the requester</param>
        public void AddRequestContext(string hostname, User user, string path, string method, string ipAddress) {
            _context.AddHostname(hostname);
            if (user != null)
                _context.AddUser(user, ipAddress);
            _context.AddRoute(path, method);
            _context.AddRequest();
        }


        // Main event processing loop that handles scheduled events and queued events
        private async Task ProcessEventsAsync()
        {
            var currentSecond = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var requestsThisSecond = 0;

            while (!_cancellationSource.Token.IsCancellationRequested)
            {
                try
                {
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

                var (token, evt, interval, nextRun, callback) = kvp.Value;
                if (now >= nextRun)
                {
                    QueueEvent(token, evt.Invoke(), callback);
                    _scheduledEvents.TryUpdate(
                        kvp.Key,
                        (token, evt, interval, nextRun + interval, callback),
                        (token, evt, interval, nextRun, callback)
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
                var delayMs = Math.Max(0, (int)((currentSecond + 1) * 1000 - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
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

        private async Task<int> ProcessSingleEvent((string token, IEvent evt, Action<IEvent> callback) eventItem, int requestsThisSecond)
        {
            var (token, evt, callback) = eventItem;
            try
            {
                requestsThisSecond++;
                var response = await _api.Reporting.ReportAsync(token, evt, _batchTimeoutMs)
                    .ConfigureAwait(false);

                if (!response.Success)
                {
                    if (response.Error == "rate_limited" || response.Error == "timeout")
                    {
                        _eventQueue.Enqueue(eventItem);
                        await Task.Delay(RetryDelayMs, _cancellationSource.Token);
                    }
                    // Other errors are dropped to avoid infinite retries
                }
                callback?.Invoke(evt);
            }
            catch (OperationCanceledException) when (_cancellationSource.Token.IsCancellationRequested)
            {
                // Graceful shutdown
                _eventQueue.Enqueue(eventItem);
                throw;
            }
            catch (Exception)
            {
                // Requeue on error and delay
                _eventQueue.Enqueue(eventItem);
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
            var heartbeat = new Heartbeat
            {
                Agent = AgentInfoHelper.GetInfo(),
                Hostnames = _context.Hostnames
                    .ToList(),
                Users = _context.Users
                    .ToList(),
                Routes = _context.Routes
                    .ToList()
            };
            heartbeat.Stats.Requests.Total = _context.Requests;
            heartbeat.Stats.Requests.Aborted = _context.RequestsAborted;
            heartbeat.Stats.Requests.AttacksDetected = new AttacksDetected
            {
                Blocked = _context.AttacksBlocked,
                Total = _context.AttacksDetected
            };
            heartbeat.Stats.StartedAt = _context.Started;
            heartbeat.Stats.EndedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            ClearContext();
            return heartbeat;
        }

	}
}
