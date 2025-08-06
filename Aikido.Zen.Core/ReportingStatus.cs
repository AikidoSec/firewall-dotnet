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
            if (operation == Heartbeat.HeartbeatEventName)
            {
                _lastReported = GetCurrentTime();
                _success = success;
            }

            if (operation == Started.StartedEventName && (_lastReported == null || _success == false))
            {
                _lastReported = GetCurrentTime();
                _success = success;
            }
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

            var now = GetCurrentTime();
            // A grace period is added to allow for slight delays in reporting
            // This helps avoid false expirations due to network latency or short processing delays
            if (_lastReported.Value.Add(Heartbeat.Interval).Add(_gracePeriod) < now)
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
