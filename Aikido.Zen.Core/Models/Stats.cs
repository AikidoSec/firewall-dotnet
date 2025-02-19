using System;
using System.Collections.Generic;
using System.Linq;

namespace Aikido.Zen.Core.Models
{
    public class Stats
    {
        private readonly int _maxPerfSamplesInMem;
        private readonly int _maxCompressedStatsInMem;
        private Dictionary<string, MonitoredSinkStats> _sinks = new Dictionary<string, MonitoredSinkStats>();
        private Requests _requests = new Requests();

        public Stats(int maxPerfSamplesInMem = 1000, int maxCompressedStatsInMem = 100)
        {
            _maxPerfSamplesInMem = maxPerfSamplesInMem;
            _maxCompressedStatsInMem = maxCompressedStatsInMem;
            Reset();
        }

        public Dictionary<string, MonitoredSinkStats> Sinks => _sinks;
        public long StartedAt { get; set; }
        public long EndedAt { get; set; }
        public Requests Requests => _requests;

        public void Reset()
        {
            _sinks = new Dictionary<string, MonitoredSinkStats>();
            _requests = new Requests
            {
                Total = 0,
                Aborted = 0,
                AttacksDetected = new AttacksDetected
                {
                    Total = 0,
                    Blocked = 0
                }
            };
            StartedAt = DateTime.UtcNow.Ticks;
        }

        public bool HasCompressedStats()
        {
            return _sinks.Any(s => s.Value.CompressedTimings.Any());
        }

        public void InterceptorThrewError(string sink)
        {
            EnsureSinkStats(sink);
            _sinks[sink].Total++;
            _sinks[sink].InterceptorThrewError++;
        }

        public void OnDetectedAttack(bool blocked)
        {
            _requests.AttacksDetected.Total++;
            if (blocked)
            {
                _requests.AttacksDetected.Blocked++;
            }
        }

        public void ForceCompress()
        {
            foreach (var sink in _sinks.Keys)
            {
                CompressPerfSamples(sink);
            }
        }

        private void EnsureSinkStats(string sink)
        {
            if (!_sinks.ContainsKey(sink))
            {
                _sinks[sink] = new MonitoredSinkStats();
            }
        }

        private void CompressPerfSamples(string sink)
        {
            if (!_sinks.ContainsKey(sink) || _sinks[sink].Durations.Count == 0)
            {
                return;
            }

            var timings = _sinks[sink].Durations;
            var averageInMs = timings.Average();

            // Calculate percentiles
            var sortedTimings = timings.OrderBy(t => t).ToList();
            var percentiles = new Dictionary<string, double>
            {
                ["50"] = CalculatePercentile(sortedTimings, 50),
                ["75"] = CalculatePercentile(sortedTimings, 75),
                ["90"] = CalculatePercentile(sortedTimings, 90),
                ["95"] = CalculatePercentile(sortedTimings, 95),
                ["99"] = CalculatePercentile(sortedTimings, 99)
            };

            _sinks[sink].CompressedTimings.Add(new CompressedTiming
            {
                AverageInMS = averageInMs,
                Percentiles = percentiles,
                CompressedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });

            if (_sinks[sink].CompressedTimings.Count > _maxCompressedStatsInMem)
            {
                _sinks[sink].CompressedTimings.RemoveAt(0);
            }

            _sinks[sink].Durations.Clear();
        }

        private static double CalculatePercentile(List<double> sortedValues, int percentile)
        {
            if (sortedValues.Count == 0)
                return 0;

            var index = (percentile / 100.0) * (sortedValues.Count - 1);
            var lower = (int)Math.Floor(index);
            var upper = (int)Math.Ceiling(index);

            if (lower == upper)
                return sortedValues[lower];

            var weight = index - lower;
            var result = (1 - weight) * sortedValues[lower] + weight * sortedValues[upper];
            return Math.Round(result, 2);
        }

        public bool IsEmpty()
        {
            return !_sinks.Any() &&
                   _requests.Total == 0 &&
                   _requests.AttacksDetected.Total == 0;
        }

        public void OnInspectedCall(string sink, bool blocked, bool attackDetected, double durationInMs, bool withoutContext)
        {
            EnsureSinkStats(sink);
            var sinkStats = _sinks[sink];
            sinkStats.Total++;

            if (withoutContext)
            {
                sinkStats.WithoutContext++;
                return;
            }

            if (sinkStats.Durations.Count >= _maxPerfSamplesInMem)
            {
                CompressPerfSamples(sink);
            }

            sinkStats.Durations.Add(durationInMs);

            if (attackDetected)
            {
                sinkStats.AttacksDetected.Total++;
                if (blocked)
                {
                    sinkStats.AttacksDetected.Blocked++;
                }
            }
        }

        public void OnRequest()
        {
            _requests.Total++;
        }

        public void OnAbortedRequest()
        {
            _requests.Aborted++;
        }

        public StatsSnapshot GetStats()
        {
            return new StatsSnapshot
            {
                Sinks = _sinks,
                StartedAt = StartedAt,
                Requests = _requests
            };
        }
    }

    public class StatsSnapshot
    {
        public Dictionary<string, MonitoredSinkStats> Sinks { get; set; }
        public long StartedAt { get; set; }
        public Requests Requests { get; set; }
    }
}
