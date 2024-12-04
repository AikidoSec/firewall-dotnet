using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models.Events;

namespace Aikido.Zen.Core
{
	public class Agent : IDisposable
	{
		private readonly IZenApi _api;
		private readonly ConcurrentQueue<(string token, IEvent evt)> _eventQueue;
		private readonly CancellationTokenSource _cancellationSource;
		private readonly Task _backgroundTask;
		private readonly int _batchTimeoutMs;
		private readonly ConcurrentDictionary<string, (IEvent evt, TimeSpan interval, DateTime nextRun)> _scheduledEvents;
		private const int RateLimitPerSecond = 10;
		private const int RetryDelayMs = 250;
		private const int EmptyQueueDelayMs = 100;
		private const int ErrorRetryDelayMs = 1000;
		private const string HeartbeatScheduleId = "heartbeat";
		private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromMinutes(10);

		public Agent(IZenApi api, int batchTimeoutMs = 5000)
		{
			_api = api ?? throw new ArgumentNullException(nameof(api));
			_eventQueue = new ConcurrentQueue<(string token, IEvent evt)>();
			_scheduledEvents = new ConcurrentDictionary<string, (IEvent, TimeSpan, DateTime)>();
			_cancellationSource = new CancellationTokenSource();
			_batchTimeoutMs = batchTimeoutMs;
			_backgroundTask = Task.Run(ProcessEventsAsync);
		}

        public void Start(string token) {
            QueueEvent(token, new Started {
                Agent = AgentInfoHelper.GetInfo()			
            });

            // Schedule heartbeat event every 10 minutes
            ScheduleEvent(token, new Heartbeat {
				Agent = AgentInfoHelper.GetInfo()
			}, HeartbeatInterval, HeartbeatScheduleId);
        }

		public void QueueEvent(string token, IEvent evt)
		{
			if (string.IsNullOrEmpty(token)) throw new ArgumentNullException(nameof(token));
			if (evt == null) throw new ArgumentNullException(nameof(evt));
			
			_eventQueue.Enqueue((token, evt));
		}

		public void ScheduleEvent(string token, IEvent evt, TimeSpan interval, string scheduleId)
		{
			if (string.IsNullOrEmpty(token)) throw new ArgumentNullException(nameof(token));
			if (evt == null) throw new ArgumentNullException(nameof(evt));
			if (string.IsNullOrEmpty(scheduleId)) throw new ArgumentNullException(nameof(scheduleId));
			if (interval <= TimeSpan.Zero) throw new ArgumentException("Interval must be positive", nameof(interval));

            _scheduledEvents.AddOrUpdate(
                scheduleId,
                (evt, interval, DateTime.UtcNow + interval),
                (_, __) => (evt, interval, DateTime.UtcNow + interval)
            );
		}

		public void RemoveScheduledEvent(string scheduleId)
		{
			if (string.IsNullOrEmpty(scheduleId)) throw new ArgumentNullException(nameof(scheduleId));
			_scheduledEvents.TryRemove(scheduleId, out _);
		}

		private async Task ProcessEventsAsync()
		{
			var currentSecond = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			var requestsThisSecond = 0;

			while (!_cancellationSource.Token.IsCancellationRequested)
			{
				try
				{
					// Process scheduled events
					var now = DateTime.UtcNow;
					foreach (var kvp in _scheduledEvents.ToArray()) // Avoid enumeration issues
					{
						if (_cancellationSource.Token.IsCancellationRequested) break;

						var (evt, interval, nextRun) = kvp.Value;
						if (now >= nextRun)
						{
							QueueEvent(kvp.Key, evt);
							_scheduledEvents.TryUpdate(
								kvp.Key,
								(evt, interval, nextRun + interval),
								(evt, interval, nextRun)
							);
						}
					}

					// Check rate limit
					var thisSecond = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
					if (thisSecond > currentSecond)
					{
						currentSecond = thisSecond;
						requestsThisSecond = 0;
					}

					if (requestsThisSecond >= RateLimitPerSecond)
					{
						var delayMs = Math.Max(0, (int)((currentSecond + 1) * 1000 - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
						await Task.Delay(delayMs, _cancellationSource.Token);
						continue;
					}

					// Process queued events
					if (_eventQueue.TryDequeue(out var eventItem))
					{
						var (token, evt) = eventItem;
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
						}
						catch (OperationCanceledException) when (_cancellationSource.Token.IsCancellationRequested)
						{
							// Graceful shutdown
							_eventQueue.Enqueue(eventItem);
							break;
						}
						catch (Exception)
						{
							// Requeue on error and delay
							_eventQueue.Enqueue(eventItem);
							await Task.Delay(RetryDelayMs, _cancellationSource.Token);
						}
					}
					else
					{
						await Task.Delay(EmptyQueueDelayMs, _cancellationSource.Token);
					}
				}
				catch (OperationCanceledException) when (_cancellationSource.Token.IsCancellationRequested)
				{
					break;
				}
				catch (Exception)
				{
					// Ensure the loop continues even if an unexpected error occurs
					try
					{
						await Task.Delay(ErrorRetryDelayMs, _cancellationSource.Token);
					}
					catch (OperationCanceledException) when (_cancellationSource.Token.IsCancellationRequested)
					{
						break;
					}
				}
			}
		}

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
	}
}
