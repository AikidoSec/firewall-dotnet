using System.Collections.Concurrent;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models.Events;
using Aikido.Zen.Tests.Mocks;
using Microsoft.Extensions.Logging;
using Moq;

namespace Aikido.Zen.Test
{
    [NonParallelizable]
    public class CustomEventAdmissionTests
    {
        private Agent? _agent;
        private ConcurrentQueue<CustomEvent> _reported = null!;
        private Mock<IReportingAPIClient> _reporting = null!;
        private Mock<ILogger> _logger = null!;
        private ILogger _originalLogger = null!;
        private string? _originalToken;
        private string? _originalDebug;

        [SetUp]
        public void SetUp()
        {
            LogHelper.Reset();
            _originalToken = Environment.GetEnvironmentVariable("AIKIDO_TOKEN");
            _originalDebug = Environment.GetEnvironmentVariable("AIKIDO_DEBUG");
            _originalLogger = Agent.Logger;
            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", "test-token");
            Environment.SetEnvironmentVariable("AIKIDO_DEBUG", null);
            _logger = new Mock<ILogger>();
            Agent.ConfigureLogger(_logger.Object);
            _reported = new ConcurrentQueue<CustomEvent>();
            _reporting = new Mock<IReportingAPIClient>();
            _reporting
                .Setup(api => api.ReportAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .Callback<string, object, CancellationToken>((_, evt, _) => _reported.Enqueue((CustomEvent)evt))
                .ReturnsAsync(new ReportingAPIResponse { Success = true });
            _agent = new Agent(ZenApiMock.CreateMock(_reporting.Object).Object);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                DisposeAgent();
            }
            finally
            {
                Agent.ConfigureLogger(_originalLogger);
                Environment.SetEnvironmentVariable("AIKIDO_TOKEN", _originalToken);
                Environment.SetEnvironmentVariable("AIKIDO_DEBUG", _originalDebug);
            }
        }

        [TestCase(null)]
        [TestCase("")]
        public void SendCustomEvent_WithoutToken_DoesNotReport(string? token)
        {
            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", token);

            _agent!.SendCustomEvent("user.login_failed", new Context());
            DisposeAgent();

            VerifyNothingReported();
        }

        [TestCase(null)]
        [TestCase("")]
        public void SendCustomEvent_WithoutEventName_DoesNotReport(string? eventName)
        {
            _agent!.SendCustomEvent(eventName, new Context());
            DisposeAgent();

            VerifyNothingReported();
        }

        [Test]
        public void SendCustomEvent_WithoutContext_WarnsOnceAcrossConcurrentCalls()
        {
            Parallel.For(0, 20, _ => _agent!.SendCustomEvent("user.login_failed", null));
            DisposeAgent();

            VerifyNothingReported();
            _logger.Verify(logger => logger.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((value, _) => value.ToString()!.Contains("was called without a context")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        [Test]
        public void SendCustomEvent_WithBypassedContext_DoesNotReport()
        {
            _agent!.SendCustomEvent("user.login_failed", new Context { Bypassed = true });
            DisposeAgent();

            VerifyNothingReported();
        }

        [Test]
        public void SendCustomEvent_ReportsFirst25CallsAcrossNamesAndWarnsOnce()
        {
            var context = new Context();
            var names = Enumerable.Range(0, 40)
                .Select(i => i % 2 == 0 ? "repeated" : $"event-{i}").ToArray();

            foreach (var name in names)
            {
                _agent!.SendCustomEvent(name, context);
            }
            DisposeAgent();

            Assert.That(_reported.Select(evt => evt.Name), Is.EquivalentTo(names.Take(25)));
            VerifyLimitWarnings(Times.Once());
        }

        [Test]
        public void SendCustomEvent_EachRequestHasItsOwnAllowanceAndWarning()
        {
            for (var request = 0; request < 3; request++)
            {
                var context = new Context { Route = $"/request-{request}" };
                for (var i = 0; i < 30; i++)
                {
                    _agent!.SendCustomEvent("event", context);
                }
            }
            DisposeAgent();

            Assert.That(_reported.GroupBy(evt => evt.Request.Route)
                .Select(group => (group.Key, Count: group.Count())), Is.EquivalentTo(
                    Enumerable.Range(0, 3).Select(i => ($"/request-{i}", Count: 25))));
            VerifyLimitWarnings(Times.Exactly(3));
        }

        [Test]
        public void SendCustomEvent_ConcurrentCallsShareTheRequestAllowance()
        {
            var context = new Context();

            Parallel.For(0, 1000, i => _agent!.SendCustomEvent($"event-{i}", context));
            DisposeAgent();

            Assert.Multiple(() =>
            {
                Assert.That(_reported, Has.Count.EqualTo(25));
                Assert.That(_reported.Select(evt => evt.Name).Distinct().Count(), Is.EqualTo(25));
            });
            VerifyLimitWarnings(Times.Once());
        }

        [TestCase("missing-token")]
        [TestCase("bypassed")]
        [TestCase("empty-name")]
        [TestCase("null-name")]
        public void SendCustomEvent_SkippedCallsDoNotConsumeRequestAllowance(string reason)
        {
            var context = new Context { Bypassed = reason == "bypassed" };
            var name = reason == "empty-name" ? "" : reason == "null-name" ? null : "event";
            if (reason == "missing-token")
            {
                Environment.SetEnvironmentVariable("AIKIDO_TOKEN", null);
            }
            for (var i = 0; i < 30; i++)
            {
                _agent!.SendCustomEvent(name, context);
            }
            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", "test-token");
            context.Bypassed = false;
            for (var i = 0; i < 25; i++)
            {
                _agent!.SendCustomEvent($"accepted-{i}", context);
            }
            DisposeAgent();

            Assert.That(_reported.Select(evt => evt.Name), Is.EquivalentTo(
                Enumerable.Range(0, 25).Select(i => $"accepted-{i}")));
            VerifyLimitWarnings(Times.Never());
        }

        [Test]
        public void SendCustomEvent_WhenLimitWarningThrows_StillDropsExcessEventsWithoutThrowing()
        {
            var context = new Context();
            _logger.Setup(logger => logger.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
                .Throws(new InvalidOperationException("Logger failed"));

            for (var i = 0; i < 40; i++)
            {
                Assert.DoesNotThrow(() => _agent!.SendCustomEvent($"event-{i}", context));
            }
            DisposeAgent();

            Assert.That(_reported.Select(evt => evt.Name), Is.EquivalentTo(
                Enumerable.Range(0, 25).Select(i => $"event-{i}")));
            VerifyLimitWarnings(Times.Once());
        }

        private void VerifyLimitWarnings(Times times)
        {
            _logger.Verify(logger => logger.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((value, _) => value.ToString()!.Contains("25 custom events")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), times);
        }

        private void DisposeAgent()
        {
            _agent?.Dispose();
            _agent = null;
        }

        private void VerifyNothingReported()
        {
            _reporting.Verify(api => api.ReportAsync(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
