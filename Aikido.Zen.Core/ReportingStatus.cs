using System;
using Aikido.Zen.Core.Models.Events;

namespace Aikido.Zen.Core
{
    internal class ReportingStatus
    {
        private DateTime? _lastReported;
        private bool _success;
        private readonly TimeSpan _gracePeriod = TimeSpan.FromSeconds(30);

        public void SignalReporting(string operation, bool success)
        {
            // The last reported time and success status can be set in 2 cases:
            // 1. Initial reporting (Started event)
            // 2. Heartbeat event

            if (operation != Heartbeat.HeartbeatEventName && operation != Started.StartedEventName)
            {
                // Ignore other operations
                return;
            }

            _lastReported = GetCurrentTime();
            _success = success;
        }

        public ReportingStatusResult GetReportingStatus()
        {
            if (_lastReported == null)
            {
                return ReportingStatusResult.NotReported;
            }

            if (!_success)
            {
                return ReportingStatusResult.Failure;
            }

            // A grace period is added to allow for slight delays in reporting
            // This helps avoid false expirations due to network latency or short processing delays
            var now = GetCurrentTime();
            var lastReportedTime = _lastReported.Value;
            var nextHeartbeatTime = lastReportedTime.Add(Heartbeat.Interval);
            if (nextHeartbeatTime.Add(_gracePeriod) < now)
            {
                return ReportingStatusResult.Expired;
            }

            return ReportingStatusResult.Ok;
        }

        protected virtual DateTime GetCurrentTime()
        {
            // This method can be overridden for testing purposes
            return DateTime.UtcNow;
        }
    }
}
