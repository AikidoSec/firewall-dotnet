using System;
using System.Threading;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Models.Events;
using NUnit.Framework;

namespace Aikido.Zen.Test
{
    [TestFixture]
    public class ReportingStatusTests
    {
        private ReportingStatus _reportingStatus;

        [SetUp]
        public void SetUp()
        {
            _reportingStatus = new ReportingStatus();
        }

        [Test]
        public void GetReportingStatus_WhenNoReportsExist_ReturnsNotReported()
        {
            // Act
            var result = _reportingStatus.GetReportingStatus();

            // Assert
            Assert.That(result, Is.EqualTo(ReportingStatusResult.NotReported));
        }

        [Test]
        public void SignalReporting_WithHeartbeatEventSuccess_GetReportingStatusReturnsOk()
        {
            // Act
            _reportingStatus.OnEventReported(Heartbeat.EventType, true);
            var result = _reportingStatus.GetReportingStatus();

            // Assert
            Assert.That(result, Is.EqualTo(ReportingStatusResult.Ok));
        }

        [Test]
        public void SignalReporting_StartedRetriedAfterFailure_GetReportingStatusReturnsOk()
        {
            // Arrange
            _reportingStatus.OnEventReported(Started.EventType, false);
            _reportingStatus.OnEventReported(Started.EventType, true);

            // Act
            var result = _reportingStatus.GetReportingStatus();

            // Assert
            Assert.That(result, Is.EqualTo(ReportingStatusResult.Ok));
        }

        [Test]
        public void SignalReporting_WithStartedEventSuccess_GetReportingStatusReturnsOk()
        {
            // Act
            _reportingStatus.OnEventReported(Started.EventType, true);
            var result = _reportingStatus.GetReportingStatus();

            // Assert
            Assert.That(result, Is.EqualTo(ReportingStatusResult.Ok));
        }

        [Test]
        public void SignalReporting_WithHeartbeatEventFailure_GetReportingStatusReturnsFailure()
        {
            // Act
            _reportingStatus.OnEventReported(Heartbeat.EventType, false);
            var result = _reportingStatus.GetReportingStatus();

            // Assert
            Assert.That(result, Is.EqualTo(ReportingStatusResult.Failure));
        }

        [Test]
        public void SignalReporting_WithStartedEventFailure_GetReportingStatusReturnsFailure()
        {
            // Act
            _reportingStatus.OnEventReported(Started.EventType, false);
            var result = _reportingStatus.GetReportingStatus();

            // Assert
            Assert.That(result, Is.EqualTo(ReportingStatusResult.Failure));
        }

        [Test]
        public void SignalReporting_WithOtherEventSuccess_GetReportingStatusReturnsNotReported()
        {
            // Act
            _reportingStatus.OnEventReported("some_other_event", true);
            var result = _reportingStatus.GetReportingStatus();

            // Assert
            Assert.That(result, Is.EqualTo(ReportingStatusResult.NotReported));
        }

        [Test]
        public void GetReportingStatus_WhenHeartbeatExpired_ReturnsExpired()
        {
            // Arrange
            var baseTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var testableReportingStatus = new TestableReportingStatus(baseTime);
            
            // Signal successful reporting at base time
            testableReportingStatus.OnEventReported(Heartbeat.EventType, true);
            
            // Move time forward beyond heartbeat interval + grace period
            var expiredTime = baseTime.Add(Heartbeat.Interval).AddSeconds(31); // Grace period is 30 seconds
            testableReportingStatus.SetCurrentTime(expiredTime);

            // Act
            var result = testableReportingStatus.GetReportingStatus();

            // Assert
            Assert.That(result, Is.EqualTo(ReportingStatusResult.Expired));
        }

        [Test]
        public void GetReportingStatus_WhenStartedExpired_ReturnsExpired()
        {
            // Arrange
            var baseTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var testableReportingStatus = new TestableReportingStatus(baseTime);
            
            // Signal successful reporting at base time (only Started event, no Heartbeat)
            testableReportingStatus.OnEventReported(Started.EventType, true);
            
            // Move time forward beyond heartbeat interval + grace period
            var expiredTime = baseTime.Add(Heartbeat.Interval).AddSeconds(31); // Grace period is 30 seconds
            testableReportingStatus.SetCurrentTime(expiredTime);

            // Act
            var result = testableReportingStatus.GetReportingStatus();

            // Assert
            Assert.That(result, Is.EqualTo(ReportingStatusResult.Expired));
        }

        [Test]
        public void GetReportingStatus_WhenWithinGracePeriod_ReturnsOk()
        {
            // Arrange
            var baseTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            var testableReportingStatus = new TestableReportingStatus(baseTime);
            
            // Signal successful reporting at base time
            testableReportingStatus.OnEventReported(Heartbeat.EventType, true);
            
            // Move time forward but still within grace period
            var withinGracePeriodTime = baseTime.Add(Heartbeat.Interval).AddSeconds(15); // Grace period is 30 seconds
            testableReportingStatus.SetCurrentTime(withinGracePeriodTime);

            // Act
            var result = testableReportingStatus.GetReportingStatus();

            // Assert
            Assert.That(result, Is.EqualTo(ReportingStatusResult.Ok));
        }

        [Test]
        public void SignalReporting_WithMultipleOperations_PrefersHeartbeatOverStarted()
        {
            // Arrange
            _reportingStatus.OnEventReported(Started.EventType, false);
            _reportingStatus.OnEventReported(Heartbeat.EventType, true);

            // Act
            var result = _reportingStatus.GetReportingStatus();

            // Assert
            Assert.That(result, Is.EqualTo(ReportingStatusResult.Ok));
        }

        [Test]
        public void SignalReporting_WithStartedFailureThenHeartbeatSuccess_ReturnsOk()
        {
            // Arrange
            _reportingStatus.OnEventReported(Started.EventType, false);
            _reportingStatus.OnEventReported(Heartbeat.EventType, true);

            // Act
            var result = _reportingStatus.GetReportingStatus();

            // Assert
            Assert.That(result, Is.EqualTo(ReportingStatusResult.Ok));
        }

        [Test]
        public void SignalReporting_OverwritesPreviousStatusForSameOperation()
        {
            // Arrange
            _reportingStatus.OnEventReported(Heartbeat.EventType, false);
            
            // Act - overwrite with success
            _reportingStatus.OnEventReported(Heartbeat.EventType, true);
            var result = _reportingStatus.GetReportingStatus();

            // Assert
            Assert.That(result, Is.EqualTo(ReportingStatusResult.Ok));
        }

        [Test]
        public void SignalReporting_WithEmptyOperation_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _reportingStatus.OnEventReported("", true));
            Assert.DoesNotThrow(() => _reportingStatus.OnEventReported(string.Empty, false));
            Assert.DoesNotThrow(() => _reportingStatus.OnEventReported(null, false));
        }

        [Test]
        public void GetReportingStatus_WhenOnlyStartedEventExists_UsesStartedEvent()
        {
            // Act
            _reportingStatus.OnEventReported(Started.EventType, true);
            var result = _reportingStatus.GetReportingStatus();

            // Assert
            Assert.That(result, Is.EqualTo(ReportingStatusResult.Ok));
        }

        [Test]
        public void GetReportingStatus_WhenOnlyStartedEventExistsAndFailed_ReturnsFailure()
        {
            // Act
            _reportingStatus.OnEventReported(Started.EventType, false);
            var result = _reportingStatus.GetReportingStatus();

            // Assert
            Assert.That(result, Is.EqualTo(ReportingStatusResult.Failure));
        }

        [Test]
        public void GetReportingStatus_ConcurrentAccess_ThreadSafe()
        {
            // Arrange
            var results = new ReportingStatusResult[10];
            var threads = new Thread[10];

            // Act
            for (int i = 0; i < 10; i++)
            {
                int index = i;
                threads[i] = new Thread(() =>
                {
                    _reportingStatus.OnEventReported(Heartbeat.EventType, true);
                    results[index] = _reportingStatus.GetReportingStatus();
                });
                threads[i].Start();
            }

            // Wait for all threads to complete
            foreach (var thread in threads)
            {
                thread.Join();
            }

            // Assert
            foreach (var result in results)
            {
                Assert.That(result, Is.EqualTo(ReportingStatusResult.Ok));
            }
        }

        [Test]
        public void GetReportingStatus_PrioritizesHeartbeatOverStartedEvent()
        {
            // Arrange - Signal both events with different outcomes
            _reportingStatus.OnEventReported(Started.EventType, true);
            _reportingStatus.OnEventReported(Heartbeat.EventType, false);

            // Act
            var result = _reportingStatus.GetReportingStatus();

            // Assert - Should return failure because Heartbeat has priority and failed
            Assert.That(result, Is.EqualTo(ReportingStatusResult.Failure));
        }

        [Test]
        public void SignalReporting_WithValidOperation_UpdatesReportingStatus()
        {
            // Arrange
            var customOperation = "other_operation";

            // Act
            _reportingStatus.OnEventReported(customOperation, true);

            // Assert - Custom operations don't affect GetReportingStatus (only Heartbeat and Started do)
            var result = _reportingStatus.GetReportingStatus();
            Assert.That(result, Is.EqualTo(ReportingStatusResult.NotReported));
        }

        // Testable subclass that allows controlling the current time
        private class TestableReportingStatus : ReportingStatus
        {
            private DateTime _currentTime;

            public TestableReportingStatus(DateTime initialTime)
            {
                _currentTime = initialTime;
            }

            public void SetCurrentTime(DateTime time)
            {
                _currentTime = time;
            }

            protected override DateTime GetCurrentTime()
            {
                return _currentTime;
            }
        }
    }
}
