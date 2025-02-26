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

        [TearDown]
        public void TearDown()
        {
            _agent.Dispose();
        }

        [Test]
        public void ClearContext_ResetsAllContextValues()
        {
            // Arrange
            var context = new Context
            {
                User = new User("123", "testUser"),
                Url = "/test",
                Method = "GET",
                RemoteAddress = "127.0.0.1"
            };
            _agent.CaptureInboundRequest(context);
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
            await Task.Delay(500);

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
            await Task.Delay(500);

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
            var item = new Agent.ScheduledItem
            {
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
            await Task.Delay(500); // Wait for multiple intervals

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
            // Arrange
            var context = new Context
            {
                Url = "/test",
                Method = "GET",
                RemoteAddress = "127.0.0.1"
            };

            // Act
            _agent.CaptureInboundRequest(context);

            // Assert
            Assert.That(_agent.Context.Users.Count, Is.EqualTo(0));
            Assert.That(_agent.Context.Requests, Is.EqualTo(1));
            Assert.That(_agent.Context.Routes.Count, Is.EqualTo(0));
        }

        [Test]
        public void CaptureInboundRequest_AddsToContext()
        {
            // Arrange
            var user = new User("123", "userName");
            var context = new Context
            {
                User = user,
                Url = "/test/path",
                Method = "POST",
                RemoteAddress = "192.168.1.1"
            };

            // Act
            _agent.CaptureInboundRequest(context);

            // Assert
            Assert.That(_agent.Context.Users.Count, Is.EqualTo(1));
            Assert.That(_agent.Context.Users.Any(u => u.Id == "123"));
            Assert.That(_agent.Context.Routes.Count, Is.EqualTo(0));
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
            var context = new Context
            {
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
            var context = new Context
            {
                User = new User("123", "userName"),
                Url = "/test",
                Method = "GET",
                RemoteAddress = "1.2.3.4"
            };
            _agent.Context.AddHostname("test.com");
            _agent.Context.AddUser(context.User, context.RemoteAddress);
            _agent.Context.AddRoute(context);
            _agent.Context.AddRequest();
            _agent.Context.AddAttackBlocked();
            _agent.Context.AddAttackDetected();
            _agent.SetContextMiddlewareInstalled(true);
            _agent.SetBlockingMiddlewareInstalled(true);
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
                Assert.That(heartbeat.MiddlewareInstalled, Is.True);
            });
        }

        [Test]
        public void ConstructHeartbeat_WithOnlyContextMiddlewareInstalled_SetsMiddlewareInstalledToFalse()
        {
            // Act
            _agent.SetContextMiddlewareInstalled(true);
            _agent.SetBlockingMiddlewareInstalled(false);
            var heartbeat = _agent.ConstructHeartbeat();

            // Assert
            Assert.That(heartbeat.MiddlewareInstalled, Is.False);
        }

        [Test]
        public async Task ConfigChanged_WhenConfigVersionDiffers_UpdatesConfig()
        {
            // Arrange
            var configLastUpdated = 123L;
            var newConfigLastUpdated = 124L;
            var blockedUsers = new[] { "user1", "user2" };
            var blockedUserAgents = "userAgent1|userAgent2";
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
                BlockedUserAgents = blockedUserAgents,
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
                Assert.That(response.BlockedUserAgents, Is.EquivalentTo(blockedUserAgents));
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

        [Test]
        public async Task ProcessSingleEvent_OnException_RequeuesEventAndDelays()
        {
            // Arrange
            var testEvent = new Started();
            _zenApiMock.Setup(x => x.Reporting.ReportAsync(It.IsAny<string>(), It.IsAny<IEvent>(), It.IsAny<int>()))
                .ThrowsAsync(new Exception("Test exception"));

            // Act
            _agent.QueueEvent("token", testEvent);
            await Task.Delay(Agent.RetryDelayMs + 250); // Wait for retry

            // Assert
            _zenApiMock.Verify(x => x.Reporting.ReportAsync(It.IsAny<string>(), It.IsAny<IEvent>(), It.IsAny<int>()),
                Times.AtLeast(2)); // Should try at least twice
        }

        [Test]
        public async Task UpdateBlockedIps_WithEmptyToken_ReturnsWithoutUpdating()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", "");

            // Act
            await _agent.UpdateBlockedIps();

            // Assert
            _zenApiMock.Verify(x => x.Reporting.GetFirewallLists(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void AddRoute_AddsRouteToContext()
        {
            // Arrange
            var context = new Context
            {
                Url = "/test/route",
                Method = "GET",
                RemoteAddress = "192.168.1.1"
            };

            // Act
            _agent.AddRoute(context);

            // Assert
            Assert.That(_agent.Context.Routes.Count, Is.EqualTo(1));
            Assert.That(_agent.Context.Routes.Any(r => r.Path == "/test/route"));
        }

        [Test]
        public void UpdateConfig_ShouldUpdateBypassedIPs()
        {
            // Arrange
            var block = true;
            var blockedUsers = new[] { "user1" };
            var bypassedIPs = new[] { "192.168.1.0/24", "10.0.0.0/8" };
            var endpoints = new[]
            {
                new EndpointConfig
                {
                    Method = "GET",
                    Route = "test",
                    AllowedIPAddresses = new[] { "172.16.0.0/16" }
                }
            };
            var configVersion = 123L;

            // Create config response for endpoints and other settings
            var apiResponse = new ReportingAPIResponse
            {
                Block = block,
                BlockedUserIds = blockedUsers,
                Endpoints = endpoints
                BypassedIPAddresses = bypassedIPs,
            };

            // Act
            _agent.Context.UpdateConfig(apiResponse);
            _agent.Context.UpdateFirewallLists(firewallListsResponse);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(_agent.Context.BlockList.IsBypassedIP("192.168.1.100"), Is.True);
                Assert.That(_agent.Context.BlockList.IsBypassedIP("10.10.10.10"), Is.True);
                Assert.That(_agent.Context.BlockList.IsBypassedIP("172.16.1.1"), Is.False);
            });
        }

        [Test]
        public void UpdateConfig_BypassedIP_ShouldBypassAllBlocking()
        {
            // Arrange
            var block = true;
            var blockedUsers = new[] { "user1" };
            var bypassedIPs = new[] { "192.168.1.0/24" };
            var endpoints = new[]
            {
                new EndpointConfig
                {
                    Method = "GET",
                    Route = "test",
                    AllowedIPAddresses = new[] { "10.0.0.0/8" }
                }
            };
            var configVersion = 123L;
            var ip = "192.168.1.100";
            var url = "GET|test";

            // Create config response for endpoints and other settings
            var apiResponse = new ReportingAPIResponse
            {
                Block = block,
                BlockedUserIds = blockedUsers,
                Endpoints = endpoints
            };

            // Create firewall lists response for bypassed IPs
            var allowedIPList = new FirewallListsAPIResponse.IPList
            {
                Source = "test",
                Description = "test bypassed IPs",
                Ips = bypassedIPs
            };
            var firewallListsResponse = new FirewallListsAPIResponse(
                allowedIPAddresses: new[] { allowedIPList }
            );

            // Act
            _agent.Context.UpdateConfig(apiResponse);
            _agent.Context.UpdateFirewallLists(firewallListsResponse);
            _agent.Context.BlockList.AddIpAddressToBlocklist(ip); // Try to block the allowed IP

            // Assert
            Assert.That(_agent.Context.BlockList.IsBlocked(ip, url, out var reason), Is.False, "Bypassed IP should bypass all blocking");
        }

        [Test]
        public void UpdateConfig_BypassedIPs_ShouldBypassEndpointRestrictions()
        {
            // Arrange
            var block = true;
            var blockedUsers = Array.Empty<string>();
            var bypassedIPs = new[] { "124.124.1.1/24" };
            var endpoints = new[]
            {
                new EndpointConfig
                {
                    Method = "GET",
                    Route = "test",
                    AllowedIPAddresses = new[] { "124.124.1.1/8" } // Different subnet
                }
            };
            var configVersion = 123L;
            var ip = "124.124.1.100";
            var url = "GET|test";

            // Create config response for endpoints and other settings
            var apiResponse = new ReportingAPIResponse
            {
                Block = block,
                BlockedUserIds = blockedUsers,
                Endpoints = endpoints,
                BypassedIPAddresses = bypassedIPs
            };

            // Create firewall lists response for bypassed IPs
            var allowedIPList = new FirewallListsAPIResponse.IPList
            {
                Source = "test",
                Description = "test bypassed IPs",
                Ips = bypassedIPs
            };
            var firewallListsResponse = new FirewallListsAPIResponse(
                allowedIPAddresses: new[] { allowedIPList }
            );

            // Act
            _agent.Context.UpdateConfig(apiResponse);
            _agent.Context.UpdateFirewallLists(firewallListsResponse);

            // Assert
            Assert.That(_agent.Context.BlockList.IsBypassedIP(ip), Is.True, "Bypassed IP should bypass endpoint restrictions");
        }

        [Test]
        public void UpdateConfig_ShouldClearBypassedIPsWhenEmpty()
        {
            // Arrange
            var block = true;
            var blockedUsers = Array.Empty<string>();
            var bypassedIPs = new[] { "192.168.1.0/24" };
            var endpoints = Array.Empty<EndpointConfig>();
            var configVersion = 123L;
            var ip = "192.168.1.100";

            // Create config response for endpoints and other settings
            var apiResponse = new ReportingAPIResponse
            {
                Block = block,
                BlockedUserIds = blockedUsers,
                Endpoints = endpoints
                BypassedIPAddresses = bypassedIPs,
            };

            // First update with bypassed IPs
            _agent.Context.UpdateConfig(apiResponse);
            _agent.Context.UpdateFirewallLists(firewallListsResponse);
            Assert.That(_agent.Context.BlockList.IsBypassedIP(ip), Is.True, "IP should be bypassed initially");

            // Act - update with empty bypassed IPs
            apiResponse.BypassedIPAddresses = new List<string>();
            _agent.Context.UpdateConfig(apiResponse);

            // Assert
            Assert.That(_agent.Context.BlockList.IsBypassedIP(ip), Is.False, "Bypassed ip list should be cleared");
        }

        [Test]
        public void UpdateConfig_ShouldHandleBypassedIpsingCorrectly()
        {
            // Arrange
            var block = true;
            var blockedUsers = new[] { "user1" };
            var bypassedIPs = new[] { "192.168.1.0/24", "10.0.0.0/8", "124.124.1.1" };
            var endpoints = new[]
            {
                new EndpointConfig
                {
                    Method = "GET",
                    Route = "test",
                    AllowedIPAddresses = new[] { "172.16.0.0/24" }
                }
            };
            var configVersion = 123L;
            var ip = "124.124.1.1";
            var url = "GET|test";

            // Create config response for endpoints and other settings
            var apiResponse = new ReportingAPIResponse
            {
                Block = block,
                BlockedUserIds = blockedUsers,
                Endpoints = endpoints
                BypassedIPAddresses = bypassedIPs,
            };

            // Create firewall lists response for bypassed IPs
            var allowedIPList = new FirewallListsAPIResponse.IPList
            {
                Source = "test",
                Description = "test bypassed IPs",
                Ips = bypassedIPs
            };

            // Act - Initial config with bypassed ips
            _agent.Context.UpdateConfig(apiResponse);
            _agent.Context.UpdateFirewallLists(firewallListsResponse);

            // Assert - Check allowed ips functionality
            Assert.Multiple(() =>
            {
                // Basic allowed ips checks
                Assert.That(_agent.Context.BlockList.IsBypassedIP("192.168.1.100"), Is.True, "IP in first subnet should be allowed");
                Assert.That(_agent.Context.BlockList.IsBypassedIP("10.10.10.10"), Is.True, "IP in second subnet should be allowed");
                Assert.That(_agent.Context.BlockList.IsBypassedIP("172.16.1.1"), Is.False, "IP not in any subnet should not be allowed");

                // Verify allowed ips bypasses all blocking
                _agent.Context.BlockList.AddIpAddressToBlocklist(ip);
                Assert.That(_agent.Context.BlockList.IsBlocked(ip, url, out var reason), Is.False, "Allowed IP should bypass blocklist");
                Assert.That(_agent.Context.BlockList.IsBlocked(ip, "GET|other", out reason), Is.False, "Allowed IP should bypass endpoint restrictions");
                Assert.That(_agent.Context.BlockList.IsBlocked(ip, "POST|test", out reason), Is.False, "Allowed IP should bypass method restrictions");

                // Verify non-allowed IPs are still subject to blocking
                _agent.Context.BlockList.AddIpAddressToBlocklist("123.123.1.1");
                Assert.That(_agent.Context.BlockList.IsBlocked("123.123.1.1", url, out reason), Is.True, "Non-allowed IP should still be subject to endpoint restrictions");
            });

            // Act - Update config to clear bypassed ips
            _agent.Context.UpdateFirewallLists(new FirewallListsAPIResponse());
            apiResponse.BypassedIPAddresses = new List<string>();
            _agent.Context.UpdateConfig(apiResponse);

            // Assert - Verify bypassed ips is cleared and blocking is restored
            Assert.Multiple(() =>
            {
                Assert.That(_agent.Context.BlockList.IsBypassedIP(ip), Is.False, "bypassed ips should be cleared");
                Assert.That(_agent.Context.BlockList.IsBlocked(ip, url, out var reason), Is.True, "IP should be blocked after bypassed ips is cleared");
            });
        }
    }
}
