using System;
using System.Collections.Generic;
using System.Linq;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core.Vulnerabilities
{
    public class AttackWaveDetector
    {
        private readonly LRUCache<string, SuspiciousState> _suspiciousRequests;
        private readonly LRUCache<string, long> _sentEventsMap;
        private readonly int _attackWaveThreshold;
        private readonly int _maxSamplesPerIp;
        private readonly object _lock = new object();

        public AttackWaveDetector(AttackWaveDetectorOptions options = null)
        {
            if (options == null)
            {
                options = new AttackWaveDetectorOptions();
            }

            var attackWaveTimeFrame = options.AttackWaveTimeFrame ?? 60 * 1000;
            var minTimeBetweenEvents = options.MinTimeBetweenEvents ?? 20 * 60 * 1000;
            var maxLruEntries = options.MaxLRUEntries ?? 10_000;

            _attackWaveThreshold = options.AttackWaveThreshold ?? 15;
            _maxSamplesPerIp = Math.Min(options.MaxSamplesPerIP ?? 15, _attackWaveThreshold);

            _suspiciousRequests = new LRUCache<string, SuspiciousState>(maxLruEntries, attackWaveTimeFrame);
            _sentEventsMap = new LRUCache<string, long>(maxLruEntries, minTimeBetweenEvents);
        }

        /// <summary>
        /// Checks if the request is part of an attack wave.
        /// Returns true when an attack wave should be reported.
        /// </summary>
        public bool Check(Context context)
        {
            // Must have remote address to proceed
            if (string.IsNullOrWhiteSpace(context.RemoteAddress))
            {
                return false;
            }

            // Requests are handled in parallel, we need to lock the cache to prevent race conditions
            lock (_lock)
            {
                var ip = context.RemoteAddress;

                // Avoid duplicate reports within cooldown period
                if (_sentEventsMap.TryGetValue(ip, out _))
                {
                    return false;
                }

                // Need enough HTTP info
                if (string.IsNullOrEmpty(context.Method) || (string.IsNullOrEmpty(context.Route) && string.IsNullOrEmpty(context.Url)))
                {
                    return false;
                }

                // Run probe detector logic
                if (!AttackWaveProbe.IsProbeRequest(context))
                {
                    return false;
                }

                // Update total counter and track unique sample
                var suspiciousRequests = TrackSuspiciousRequest(ip, context);

                // Threshold not yet reached
                if (suspiciousRequests < _attackWaveThreshold)
                {
                    return false;
                }

                // Mark event as sent and signal detection
                _sentEventsMap.Set(ip, DateTimeHelper.UTCNowUnixMilliseconds());
                return true;
            }
        }

        public IList<SuspiciousRequest> GetSamplesForIp(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip))
            {
                return new List<SuspiciousRequest>();
            }

            lock (_lock)
            {
                if (_suspiciousRequests.TryGetValue(ip, out var state) && state?.Samples != null)
                {
                    return state.Samples.ToList();
                }
            }

            return new List<SuspiciousRequest>();
        }

        private int TrackSuspiciousRequest(string ip, Context context)
        {
            if (!_suspiciousRequests.TryGetValue(ip, out var state) || state == null)
            {
                state = new SuspiciousState();
            }

            state.Count++;

            if (state.Samples.Count < _maxSamplesPerIp)
            {
                var requestSample = new SuspiciousRequest
                {
                    Method = context.Method,
                    Url = BuildUrlWithQuery(context)
                };

                // Only store unique samples
                if (!state.Samples.Any(s => string.Equals(s.Method, requestSample.Method, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(s.Url, requestSample.Url, StringComparison.OrdinalIgnoreCase)))
                {
                    state.Samples.Add(requestSample);
                }
            }

            _suspiciousRequests.Set(ip, state);
            return state.Count;
        }

        private static string BuildUrlWithQuery(Context context)
        {
            if (context == null)
            {
                return string.Empty;
            }

            var url = context.Url ?? string.Empty;

            if (context.Query == null || context.Query.Count == 0)
            {
                return url;
            }

            var queryString = string.Join("&", context.Query
                .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kvp => $"{kvp.Key}={kvp.Value ?? string.Empty}"));

            if (string.IsNullOrEmpty(queryString))
            {
                return url;
            }

            var separator = url.Contains("?") ? "&" : "?";
            return $"{url}{separator}{queryString}";
        }
    }

    public class AttackWaveDetectorOptions
    {
        public int? AttackWaveThreshold { get; set; }
        public int? AttackWaveTimeFrame { get; set; }
        public int? MinTimeBetweenEvents { get; set; }
        public int? MaxLRUEntries { get; set; }
        public int? MaxSamplesPerIP { get; set; }
    }

    public class SuspiciousRequest
    {
        public string Method { get; set; }
        public string Url { get; set; }
    }

    internal class SuspiciousState
    {
        public int Count { get; set; }
        public List<SuspiciousRequest> Samples { get; set; } = new List<SuspiciousRequest>();
    }
}
