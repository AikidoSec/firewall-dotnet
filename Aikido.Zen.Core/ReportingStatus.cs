using System;
using System.Collections.Concurrent;
using Aikido.Zen.Core.Models.Events;

namespace Aikido.Zen.Core
{
    internal class ReportingStatus
    {
        private ReportingStatusEntry? _startedReport;
        private ReportingStatusEntry? _lastHeartBeatReport;
        private readonly TimeSpan _gracePeriod = TimeSpan.FromSeconds(30);

        public void SignalReporting(string operation, bool success)
        {
            if (operation == Started.StartedEventName)
            {
                _startedReport = new ReportingStatusEntry(GetCurrentTime(), success);
            }
            else if (operation == Heartbeat.HeartbeatEventName)
            {
                _lastHeartBeatReport = new ReportingStatusEntry(GetCurrentTime(), success);
            }
        }

        public ReportingStatusResult GetReportingStatus()
        {
            var lastReport = _lastHeartBeatReport ?? _startedReport;
            if (lastReport == null)
            {
                return ReportingStatusResult.NotReported;
            }

            if (!lastReport.Value.Success)
            {
                return ReportingStatusResult.Failure;
            }

            var now = GetCurrentTime();
            // A grace period is added to allow for slight delays in reporting
            // This helps avoid false expirations due to network latency or short processing delays
            if (lastReport.Value.LastReported.Add(Heartbeat.Interval).Add(_gracePeriod) < now)
            {
                return ReportingStatusResult.Expired;
            }

            return ReportingStatusResult.Ok;
        }

        protected virtual DateTime GetCurrentTime()
        {
            return DateTime.UtcNow;
        }

        private struct ReportingStatusEntry
        {
            public ReportingStatusEntry(DateTime lastReported, bool success)
            {
                LastReported = lastReported;
                Success = success;
            }

            public DateTime LastReported { get; }
            public bool Success { get; }
        }
    }
}
