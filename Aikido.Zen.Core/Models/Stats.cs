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
            // Implementation for compressing performance samples would go here
        }

        public bool IsEmpty()
        {
            return !_sinks.Any() && 
                   _requests.Total == 0 && 
                   _requests.AttacksDetected.Total == 0;
        }
    }
}
