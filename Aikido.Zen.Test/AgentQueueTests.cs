using System.Collections.Concurrent;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Models.Events;
using Aikido.Zen.Tests.Mocks;
using Moq;

namespace Aikido.Zen.Test
{
    public class AgentQueueTests
    {
        private const string Token = "test-token";
        private const int QueueLimit = 100;
        private Agent? _agent;
        private Mock<IReportingAPIClient> _reporting = null!;
        private ConcurrentQueue<IEvent> _reported = null!;
        private List<TaskCompletionSource> _heldReports = null!;

        [SetUp]
        public void SetUp()
        {
            _reported = new ConcurrentQueue<IEvent>();
            _heldReports = new List<TaskCompletionSource>();
            _reporting = new Mock<IReportingAPIClient>();
            _reporting.Setup(api => api.ReportAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .Callback<string, object, CancellationToken>((_, evt, _) => _reported.Enqueue((IEvent)evt))
                .ReturnsAsync(new ReportingAPIResponse { Success = true });
            _agent = new Agent(ZenApiMock.CreateMock(_reporting.Object).Object);
        }

        [TearDown]
        public void TearDown()
        {
            DisposeAgent();
        }

        [Test]
        public async Task FullQueue_DropsCustomAndAttackEventsButPreservesStartupAndHeartbeatCallbacks()
        {
            await HoldWorkerAsync();
            var waiting = Enumerable.Range(0, QueueLimit).Select(CreateEvent).ToArray();
            foreach (var evt in waiting)
            {
                _agent!.QueueEvent(Token, evt);
            }
            foreach (var overflow in Enumerable.Range(0, 3).Select(CreateEvent))
            {
                _agent!.QueueEvent(Token, overflow);
            }
            var callbacks = new ConcurrentQueue<(IEvent Event, bool Success)>();
            IEvent[] controlEvents = { new Started(), new Heartbeat() };
            foreach (var evt in controlEvents)
            {
                _agent!.QueueEvent(Token, evt,
                    (reportedEvent, response) => callbacks.Enqueue((reportedEvent, response.Success)));
            }
            DisposeAgent();

            Assert.Multiple(() =>
            {
                Assert.That(_reported.Skip(1), Is.EquivalentTo(waiting.Concat(controlEvents)));
                Assert.That(callbacks, Is.EquivalentTo(controlEvents.Select(evt => (evt, true))));
            });
        }

        [Test]
        public async Task ConcurrentProducers_ShareOneQueueLimit()
        {
            await HoldWorkerAsync();

            Parallel.For(0, 1000, i => _agent!.QueueEvent(Token, CreateEvent(i)));
            DisposeAgent();

            Assert.Multiple(() =>
            {
                Assert.That(_reported, Has.Count.EqualTo(QueueLimit + 1));
                Assert.That(_reported.Distinct().Count(), Is.EqualTo(QueueLimit + 1));
            });
        }

        [Test]
        public async Task Dequeue_ReopensAdmissionForTheSharedQueue()
        {
            var releaseFirst = await HoldWorkerAsync();
            var next = new DetectedAttackWave();
            var nextReport = HoldReport(next);
            var waiting = new IEvent[] { next }
                .Concat(Enumerable.Range(0, QueueLimit - 1).Select(CreateEvent)).ToArray();
            foreach (var evt in waiting)
            {
                _agent!.QueueEvent(Token, evt);
            }
            _agent!.QueueEvent(Token, new CustomEvent());

            releaseFirst.TrySetResult();
            await nextReport.Started.WaitAsync(TimeSpan.FromSeconds(2));
            var replacement = new DetectedAttack();
            _agent.QueueEvent(Token, replacement);
            _agent.QueueEvent(Token, new CustomEvent());
            DisposeAgent();

            Assert.That(_reported.Skip(1), Is.EquivalentTo(waiting.Append(replacement)));
        }

        private async Task<TaskCompletionSource> HoldWorkerAsync()
        {
            var evt = new DetectedAttack();
            var report = HoldReport(evt);
            _agent!.QueueEvent(Token, evt);
            await report.Started.WaitAsync(TimeSpan.FromSeconds(2));
            return report.Release;
        }

        private (Task Started, TaskCompletionSource Release) HoldReport(IEvent evt)
        {
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _heldReports.Add(release);
            _reporting.Setup(api => api.ReportAsync(It.IsAny<string>(), evt, It.IsAny<CancellationToken>()))
                .Returns<string, object, CancellationToken>(async (_, reportedEvent, _) =>
                {
                    _reported.Enqueue((IEvent)reportedEvent);
                    started.TrySetResult();
                    await release.Task;
                    return new ReportingAPIResponse { Success = true };
                });
            return (started.Task, release);
        }

        private static IEvent CreateEvent(int index)
        {
            return (index % 3) switch
            {
                0 => new CustomEvent(),
                1 => new DetectedAttack(),
                _ => new DetectedAttackWave(),
            };
        }

        private void DisposeAgent()
        {
            foreach (var release in _heldReports)
            {
                release.TrySetResult();
            }
            _agent?.Dispose();
            _agent = null;
        }
    }
}
