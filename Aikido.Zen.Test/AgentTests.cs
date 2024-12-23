using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Models.Events;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aikido.Zen.Test
{
    public class AgentTests
    {
        private Mock<IZenApi> _zenApiMock;
        private Mock<IReportingAPIClient> _reportingApiClientMock;
        private Mock<IRuntimeAPIClient> _runtimeApiClientMock;
        private string _originalToken = Environment.GetEnvironmentVariable("AIKIDO_TOKEN");
        private Agent _agent;
        private const int BatchTimeoutMs = 5000;

        [SetUp]
        public void Setup()
        {
            _reportingApiClientMock = new Mock<IReportingAPIClient>();
            _runtimeApiClientMock = new Mock<IRuntimeAPIClient>();
            _reportingApiClientMock.Setup(r => r.ReportAsync(It.IsAny<string>(), It.IsAny<IEvent>(), It.IsAny<int>()))
                .ReturnsAsync(new ReportingAPIResponse { Success = true });
            _runtimeApiClientMock.Setup(r => r.GetConfig(It.IsAny<string>()))
                .ReturnsAsync(new ReportingAPIResponse {  Success = true });
            _zenApiMock = new Mock<IZenApi>();
            _zenApiMock.Setup(z => z.Reporting).Returns(_reportingApiClientMock.Object);
            _zenApiMock.Setup(z => z.Runtime).Returns(_runtimeApiClientMock.Object);
            _agent = new Agent(_zenApiMock.Object, BatchTimeoutMs);
            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", "test-token");
        }

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", _originalToken);
        }


        [Test]
        public void Constructor_WithNullApi_ThrowsArgumentNullException()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new Agent(null));
            Assert.That(ex.ParamName, Is.EqualTo("api"));
        }

        [Test]
        public void Constructor_WithNegativeBatchTimeout_ThrowsArgumentException()
        {
            var ex = Assert.Throws<ArgumentException>(() => new Agent(_zenApiMock.Object, -1));
            Assert.That(ex.ParamName, Is.EqualTo("batchTimeoutMs"));
        }

        [Test]
        public async Task Start_QueuesStartedEventAndSchedulesHeartbeat()
        {
            // Arrange
            var response = new ReportingAPIResponse { Success = true };
            _reportingApiClientMock
                .Setup(r => r.ReportAsync(It.IsAny<string>(), It.IsAny<Started>(), It.IsAny<int>()))
                .ReturnsAsync(response);

            // Act
            _agent.Start();
            await Task.Delay(250); // Allow time for async operations

            // Assert - verify event was queued
            _reportingApiClientMock.Verify(
                r => r.ReportAsync(
                    It.IsAny<string>(), 
                    It.Is<Started>(s => s.GetType() == typeof(Started)),
                    BatchTimeoutMs
                ),
                Times.Once
            );
        }

        [Test]
        public void QueueEvent_WithNullToken_ThrowsArgumentNullException()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => _agent.QueueEvent(null, new Started()));
            Assert.That(ex.ParamName, Is.EqualTo("token"));
        }

        [Test]
        public void QueueEvent_WithNullEvent_ThrowsArgumentNullException() 
        {
            var ex = Assert.Throws<ArgumentNullException>(() => _agent.QueueEvent("token", null));
            Assert.That(ex.ParamName, Is.EqualTo("evt"));
        }

        [Test]
        public async Task QueueEvent_WithValidParameters_AddsEventToQueue()
        {
            // Arrange
            var testEvent = new Started();

            // Act
            _agent.QueueEvent("token", testEvent);
            await Task.Delay(100);

            // Assert
            _reportingApiClientMock.Verify(
                r => r.ReportAsync(
                    "token",
                    It.Is<Started>(e => e == testEvent),
                    BatchTimeoutMs
                ),
                Times.Once
            );
        }

        [Test]
        public void ScheduleEvent_WithNullToken_ThrowsArgumentNullException()
        {
            var item = new Agent.ScheduledItem { Token = null };
            var ex = Assert.Throws<ArgumentNullException>(() => _agent.ScheduleEvent("id", item));
            Assert.That(ex.ParamName, Is.EqualTo("item.Token"));
        }

        [Test]
        public void ScheduleEvent_WithNullScheduleId_ThrowsArgumentNullException()
        {
            var item = new Agent.ScheduledItem { Token = "token" };
            var ex = Assert.Throws<ArgumentNullException>(() => _agent.ScheduleEvent(null, item));
            Assert.That(ex.ParamName, Is.EqualTo("scheduleId"));
        }

        [Test]
        public void ScheduleEvent_WithNonPositiveInterval_ThrowsArgumentException()
        {
            var item = new Agent.ScheduledItem { 
                Token = "token",
                Interval = TimeSpan.Zero
            };
            var ex = Assert.Throws<ArgumentException>(() => _agent.ScheduleEvent("id", item));
            Assert.That(ex.Message, Does.Contain("must be positive"));
        }

        [Test]
        public async Task ScheduleEvent_WithValidParameters_ExecutesEventPeriodically()
        {
            // Arrange
            var item = new Agent.ScheduledItem
            {
                Token = "token",
                Interval = TimeSpan.FromMilliseconds(100),
                EventFactory = () => new Started()
            };

            // Act
            _agent.ScheduleEvent("test-schedule", item);
            await Task.Delay(250); // Wait for multiple intervals

            // Assert
            _reportingApiClientMock.Verify(
                r => r.ReportAsync(
                    "token",
                    It.IsAny<Started>(),
                    BatchTimeoutMs
                ),
                Times.AtLeast(2)
            );
        }

        [Test]
        public void RemoveScheduledEvent_WithNullScheduleId_ThrowsArgumentNullException()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => _agent.RemoveScheduledEvent(null));
            Assert.That(ex.ParamName, Is.EqualTo("scheduleId"));
        }

        [Test]
        public async Task RemoveScheduledEvent_StopsExecutingScheduledEvent()
        {
            // Arrange
            var scheduleId = "test-schedule";
            var item = new Agent.ScheduledItem
            {
                Token = "token",
                Interval = TimeSpan.FromMilliseconds(100),
                EventFactory = () => new Started()
            };
            _agent.ScheduleEvent(scheduleId, item);
            await Task.Delay(150);

            // Act
            _agent.RemoveScheduledEvent(scheduleId);
            await Task.Delay(150);

            // Assert - verify no more events after removal
            _reportingApiClientMock.Verify(
                r => r.ReportAsync(
                    "token",
                    It.IsAny<Started>(),
                    BatchTimeoutMs
                ),
                Times.AtMost(2)
            );
        }

        [Test]
        public void CaptureInboundRequest_WithNullUser_HandlesGracefully()
        {
            // Act
            _agent.CaptureInboundRequest(null, "/test", "GET", "127.0.0.1");

            // Assert
            Assert.That(_agent.Context.Users.Count, Is.EqualTo(0));
            Assert.That(_agent.Context.Routes.Count, Is.EqualTo(1));
            Assert.That(_agent.Context.Requests, Is.EqualTo(1));
        }

        [Test]
        public void CaptureInboundRequest_AddsToContext()
        {
            // Arrange
            var user = new User("123", "userName");
            var path = "/test/path";
            var method = "POST";
            var ip = "192.168.1.1";

            // Act
            _agent.CaptureInboundRequest(user, path, method, ip);

            // Assert
            Assert.That(_agent.Context.Users.Count, Is.EqualTo(1));
            Assert.That(_agent.Context.Users.Any(u => u.Id == "123"));
            Assert.That(_agent.Context.Routes.Count, Is.EqualTo(1));
            Assert.That(_agent.Context.Routes.Any(r => r.Path == "/test/path"));
            Assert.That(_agent.Context.Requests, Is.EqualTo(1));
        }

        [Test]
        public void CaptureOutboundRequest_WithInvalidHostname_HandlesGracefully()
        {
            // Act
            _agent.CaptureOutboundRequest(null, 443);

            // Assert
            Assert.That(_agent.Context.Hostnames.Count, Is.EqualTo(0));
        }

        [Test]
        public void CaptureOutboundRequest_AddsHostnameToContext()
        {
            // Arrange
            var host = "api.test.com";
            var port = 8080;

            // Act 
            _agent.CaptureOutboundRequest(host, port);

            // Assert
            Assert.That(_agent.Context.Hostnames.Count, Is.EqualTo(1));
            Assert.That(_agent.Context.Hostnames.Any(h => h.Hostname == "api.test.com" && h.Port == 8080));
        }

        [Test]
        public void SendAttackEvent_WithNullContext_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => 
                _agent.SendAttackEvent(AttackKind.SqlInjection, Source.Query, "payload", "operation", null, "module", null, true));
        }

        [Test]
        public async Task SendAttackEvent_QueuesDetectedAttackEvent()
        {
            // Arrange
            var kind = AttackKind.SqlInjection;
            var source = Source.Body;
            var payload = "malicious-input";
            var operation = "login";
            var context = new Context { 
                Url = "http://test.com/login",
                Method = "POST",
                Headers = new Dictionary<string, string[]>
                {
                    { "Content-Type", new[] { "application/json" } }
                }
            };
            var module = "authentication";
            var metadata = new Dictionary<string, object>
            {
                { "sql", payload }
            };
            var blocked = true;

            // Act
            _agent.SendAttackEvent(kind, source, payload, operation, context, module, metadata, blocked);
            await Task.Delay(150);

            // Assert
            _reportingApiClientMock.Verify(
                r => r.ReportAsync(
                    It.IsAny<string>(),
                    It.Is<DetectedAttack>(a =>
                        a.Attack.Kind == kind.ToJsonName() &&
                        a.Attack.Source == source.ToJsonName() &&
                        a.Attack.Payload == payload &&
                        a.Attack.Operation == operation &&
                        a.Attack.Module == module &&
                        a.Attack.Blocked == blocked &&
                        a.Request.Url == context.Url &&
                        a.Request.Method == context.Method &&
                        a.Request.Headers.ContainsKey("Content-Type") &&
                        a.Attack.Metadata.ContainsKey("sql")

                    ),
                    BatchTimeoutMs
                ),
                Times.Once
            );
        }

        [Test]
        public void Dispose_CancelsBackgroundTaskAndDisposesResources()
        {
            // Arrange
            var testEvent = new Started();
            _agent.QueueEvent("token", testEvent);

            // Act
            _agent.Dispose();
            _agent.QueueEvent("token", testEvent); // Try to queue after dispose

            // Assert - verify no more events processed after dispose
            _reportingApiClientMock.Verify(
                r => r.ReportAsync(It.IsAny<string>(), It.IsAny<IEvent>(), It.IsAny<int>()),
                Times.Once // Only the first event before dispose
            );
        }
    }
}
