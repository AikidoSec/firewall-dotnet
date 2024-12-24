using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Models.Events;
using Moq;
using Aikido.Zen.Test.Mocks;

namespace Aikido.Zen.Test
{
    public class AgentTests
    {

        private Agent _agent;
        private Mock<IZenApi> _zenApiMock;
        private const int BatchTimeoutMs = 5000;

        [SetUp]
        public void Setup()
        {
            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", "test-token");
            _zenApiMock = ZenApiMock.CreateMock();
            _agent = new Agent(_zenApiMock.Object, BatchTimeoutMs);
        }

        [Test]
        public void ClearContext_ResetsAllContextValues()
        {
            // Arrange
            var user = new User("123", "testUser");
            var path = "/test";
            var method = "GET";
            var ip = "127.0.0.1";
            _agent.CaptureInboundRequest(user, path, method, ip);
            _agent.CaptureOutboundRequest("test.com", 443);

            // Act
            _agent.ClearContext();

            // Assert
            Assert.That(_agent.Context.Users.Count, Is.EqualTo(0));
            Assert.That(_agent.Context.Routes.Count, Is.EqualTo(0));
            Assert.That(_agent.Context.Hostnames.Count, Is.EqualTo(0));
            Assert.That(_agent.Context.Requests, Is.EqualTo(0));
        }

        [Test]
        public void CaptureUser_WithValidUser_AddsToContext()
        {
            // Arrange
            var user = new User("123", "testUser");
            var ip = "127.0.0.1";

            // Act
            _agent.CaptureUser(user, ip);

            // Assert
            Assert.That(_agent.Context.Users.Count, Is.EqualTo(1));
            var capturedUser = _agent.Context.Users.First();
            Assert.That(capturedUser.Id, Is.EqualTo("123"));
            Assert.That(capturedUser.Name, Is.EqualTo("testUser"));
            Assert.That(capturedUser.LastIpAddress, Is.EqualTo(ip));
        }

        [Test]
        public void CaptureUser_WithNullUser_HandlesGracefully()
        {
            // Act
            _agent.CaptureUser(null, "127.0.0.1");

            // Assert
            Assert.That(_agent.Context.Users.Count, Is.EqualTo(0));
        }

        [Test]
        public async Task ScheduleEvent_OverloadWithEventAndInterval_AddsToScheduledEvents()
        {
            // Arrange
            var token = "test-token";
            var evt = new Started();
            var interval = TimeSpan.FromMilliseconds(100);
            var scheduleId = "test-schedule";
            _zenApiMock = ZenApiMock.CreateMock();
            _agent = new Agent(_zenApiMock.Object);

            // Act
            _agent.ScheduleEvent(token, evt, interval, scheduleId, null);
            await Task.Delay(250);

            // Assert

            _zenApiMock.Verify(
                r => r.Reporting.ReportAsync(
                    token,
                    It.Is<Started>(e => e.GetType() == typeof(Started)),
                    BatchTimeoutMs
                ),
                Times.AtLeastOnce()
            );
        }

        [Test]
        public void ScheduleEvent_OverloadWithEventAndInterval_ThrowsOnInvalidParams()
        {
            var evt = new Started();
            var interval = TimeSpan.FromMilliseconds(100);
            var scheduleId = "test-schedule";

            // Test null token
            Assert.Throws<ArgumentNullException>(() => 
                _agent.ScheduleEvent(null, evt, interval, scheduleId, null));

            // Test null event
            Assert.Throws<ArgumentNullException>(() => 
                _agent.ScheduleEvent("token", null, interval, scheduleId, null));

            // Test null scheduleId
            Assert.Throws<ArgumentNullException>(() => 
                _agent.ScheduleEvent("token", evt, interval, null, null));

            // Test non-positive interval
            Assert.Throws<ArgumentException>(() => 
                _agent.ScheduleEvent("token", evt, TimeSpan.Zero, scheduleId, null));
        }

        [Test]
        public async Task ScheduleEvent_OverloadWithEventAndInterval_ExecutesCallback()
        {
            // Arrange
            var token = "test-token";
            var evt = new Started();
            var interval = TimeSpan.FromMilliseconds(100);
            var scheduleId = "test-schedule";
            var callbackExecuted = false;

            void callback(IEvent e, APIResponse r)
            {
                callbackExecuted = true;
            }

            // Act
            _agent.ScheduleEvent(token, evt, interval, scheduleId, callback);
            await Task.Delay(250); // Wait for execution

            // Assert
            Assert.That(callbackExecuted, Is.True);
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
            _zenApiMock = ZenApiMock.CreateMock();
            _agent = new Agent(_zenApiMock.Object);

            // Act
            _agent.Start();
            await Task.Delay(250); // Allow time for async operations

            // Assert - verify event was queued
            _zenApiMock.Verify(
                r => r.Reporting.ReportAsync(
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
            _zenApiMock = ZenApiMock.CreateMock();
            _agent = new Agent(_zenApiMock.Object);

            // Act
            _agent.QueueEvent("token", testEvent);
            await Task.Delay(100);

            // Assert
            _zenApiMock.Verify(
                r => r.Reporting.ReportAsync(
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
            _zenApiMock = ZenApiMock.CreateMock();
            _agent = new Agent(_zenApiMock.Object);

            // Act
            _agent.ScheduleEvent("test-schedule", item);
            await Task.Delay(250); // Wait for multiple intervals

            // Assert
            _zenApiMock.Verify(
                r => r.Reporting.ReportAsync(
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
            _zenApiMock = ZenApiMock.CreateMock();


            // Act
            _agent.RemoveScheduledEvent(scheduleId);
            await Task.Delay(150);

            // Assert - verify no more events after removal
            _zenApiMock.Verify(
                r => r.Reporting.ReportAsync(
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
            _zenApiMock = ZenApiMock.CreateMock();
            _agent = new Agent(_zenApiMock.Object);

            // Act
            _agent.SendAttackEvent(kind, source, payload, operation, context, module, metadata, blocked);
            await Task.Delay(150);

            // Assert
            _zenApiMock.Verify(
                r => r.Reporting.ReportAsync(
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
            _zenApiMock = ZenApiMock.CreateMock();
            _agent = new Agent(_zenApiMock.Object);
            _agent.QueueEvent("token", testEvent);

            // Act
            _agent.Dispose();
            _agent.QueueEvent("token", testEvent); // Try to queue after dispose

            // Assert - verify no more events processed after dispose
            _zenApiMock.Verify(
                r => r.Reporting.ReportAsync(It.IsAny<string>(), It.IsAny<IEvent>(), It.IsAny<int>()),
                Times.Once // Only the first event before dispose
            );
        }

        [Test]
        public void ConstructHeartbeat_ReturnsCorrectHeartbeatData()
        {
            // Arrange

            _agent.Context.AddHostname("test.com");
            _agent.Context.AddUser(new User ("123", "userName"), "1.2.3.4");
            _agent.Context.AddRoute("/test", "GET");
            _agent.Context.AddRequest();
            _agent.Context.AddAttackBlocked();
            _agent.Context.AddAttackDetected();

            // Act
            var heartbeat = _agent.ConstructHeartbeat();

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(heartbeat.Hostnames.FirstOrDefault()?.Hostname, Is.EqualTo("test.com"));
                Assert.That(heartbeat.Users.Count, Is.EqualTo(1));
                Assert.That(heartbeat.Routes.FirstOrDefault()?.Path ?? "", Is.EqualTo("/test"));
                Assert.That(heartbeat.Stats.Requests.Total, Is.EqualTo(1));
                Assert.That(heartbeat.Stats.Requests.AttacksDetected.Blocked, Is.EqualTo(1));
                Assert.That(heartbeat.Stats.Requests.AttacksDetected.Total, Is.EqualTo(1));
                Assert.That(heartbeat.Stats.StartedAt, Is.GreaterThan(0));
                Assert.That(heartbeat.Stats.EndedAt, Is.GreaterThan(heartbeat.Stats.StartedAt));
            });
        }

        [Test]
        public async Task ConfigChanged_WhenConfigVersionDiffers_UpdatesConfig()
        {
            // Arrange
            var configLastUpdated = 123L;
            var newConfigLastUpdated = 124L;
            var blockedUsers = new[] { "user1", "user2" };
            var endpoints = new[]
            {
                new EndpointConfig
                {
                    Method = "GET",
                    Route = "/test",
                    AllowedIPAddresses = new[] { "192.168.1.0/24" }
                }
            };

            _agent.Context.ConfigLastUpdated = configLastUpdated;

            var configVersionResponse = new ReportingAPIResponse
            {
                Success = true,
                ConfigUpdatedAt = newConfigLastUpdated
            };

            var configResponse = new ReportingAPIResponse
            {
                Success = true,
                BlockedUserIds = blockedUsers,
                Endpoints = endpoints,
                ConfigUpdatedAt = newConfigLastUpdated
            };

            var runtimeApiClientMock = new Mock<IRuntimeAPIClient>();
            runtimeApiClientMock.Setup(x => x.GetConfigLastUpdated(It.IsAny<string>()))
                .ReturnsAsync(configVersionResponse);
            runtimeApiClientMock.Setup(x => x.GetConfig(It.IsAny<string>()))
                .ReturnsAsync(configResponse);
            _zenApiMock = ZenApiMock.CreateMock(runtime: runtimeApiClientMock.Object);
            _agent = new Agent(_zenApiMock.Object);

            // Act
            var result = _agent.ConfigChanged(out var response);
            await Task.Delay(100);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(response.Success, Is.True);
                Assert.That(response.ConfigUpdatedAt, Is.EqualTo(newConfigLastUpdated));
                Assert.That(response.BlockedUserIds, Is.EquivalentTo(blockedUsers));
                Assert.That(response.Endpoints, Is.EquivalentTo(endpoints));
            });

            _zenApiMock.Verify(x => x.Runtime.GetConfigLastUpdated(It.IsAny<string>()), Times.Once);
            _zenApiMock.Verify(x => x.Runtime.GetConfig(It.IsAny<string>()), Times.Once);
        }

        [Test]
        public async Task ConfigChanged_WhenConfigVersionSame_ReturnsFalse()
        {
            // Arrange
            var configLastUpdated = 123L;

            var configVersionResponse = new ReportingAPIResponse
            {
                Success = true,
                ConfigUpdatedAt = configLastUpdated,
            };
            var runtimeApiClientMock = new Mock<IRuntimeAPIClient>();
            runtimeApiClientMock.Setup(x => x.GetConfigLastUpdated(It.IsAny<string>()))
                .ReturnsAsync(configVersionResponse);
            runtimeApiClientMock.Setup(x => x.GetConfig(It.IsAny<string>()))
                .ReturnsAsync(configVersionResponse);
            _zenApiMock = ZenApiMock.CreateMock(runtime: runtimeApiClientMock.Object);
            _agent = new Agent(_zenApiMock.Object);
            _agent.Context.ConfigLastUpdated = configLastUpdated;

            // Act
            var result = _agent.ConfigChanged(out var response);
            await Task.Delay(100);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(response.Success, Is.True);
                Assert.That(response.ConfigUpdatedAt, Is.EqualTo(configLastUpdated));
            });

            _zenApiMock.Verify(x => x.Runtime.GetConfigLastUpdated(It.IsAny<string>()), Times.Once);
            _zenApiMock.Verify(x => x.Runtime.GetConfig(It.IsAny<string>()), Times.Never);
        }
    }
}
