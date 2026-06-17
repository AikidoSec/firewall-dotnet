using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Models.Events;
using Aikido.Zen.Tests.Mocks;
using Microsoft.Extensions.Logging;
using Moq;
using System.Collections.Concurrent;
using System.Reflection;

namespace Aikido.Zen.Test
{
    public class AgentTests
    {

        private Agent _agent;
        private Mock<IZenApi> _zenApiMock;

        [SetUp]
        public void Setup()
        {
            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", "test-token");
            _zenApiMock = ZenApiMock.CreateMock();
            _agent = new Agent(_zenApiMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            _agent.Dispose();
        }

        [Test]
        public async Task ClearContext_ResetsAllContextValues()
        {
            // Arrange
            var context = new Context
            {
                User = new User("123", "testUser"),
                Url = "http://test.com/test",
                Method = "GET",
                RemoteAddress = "127.0.0.1"
            };
            _agent.CaptureRequestUser(context);
            _agent.IncrementTotalRequestCount();
            _agent.CaptureOutboundRequest("test.com", 443);
            var startedTime = _agent.Context.Started;

            // Act
            // wait a bit to make sure some ms have passed between settings the started time and clearing the context
            await Task.Delay(25);
            _agent.ClearContext();

            // Assert
            Assert.That(_agent.Context.Users.Count, Is.EqualTo(0));

            Assert.That(_agent.Context.Config.BlockList.IsEmpty(), Is.True);
            Assert.That(_agent.Context.Config.BlockedUserAgents, Is.EqualTo(null));
            Assert.That(_agent.Context.Config.Endpoints, Is.Empty);

            Assert.That(_agent.Context.Stats.Operations, Is.Empty);
            Assert.That(_agent.Context.Requests, Is.EqualTo(0));
            Assert.That(_agent.Context.RequestsAborted, Is.EqualTo(0));
            Assert.That(_agent.Context.Stats.Requests.AttacksDetected.Blocked, Is.Zero);
            Assert.That(_agent.Context.Stats.Requests.AttacksDetected.Total, Is.Zero);
            Assert.That(_agent.Context.Started, Is.GreaterThan(startedTime));

            Assert.That(_agent.Context.AiStats.Providers, Is.Empty);

            Assert.That(_agent.Context.Hostnames, Is.Empty);

            Assert.That(_agent.Context.Users, Is.Empty);

            Assert.That(_agent.Context.Routes.Count, Is.EqualTo(0));

            Assert.That(_agent.Context.Packages, Is.Empty);

            Assert.That(_agent.Context.Config.ConfigLastUpdated, Is.Zero);
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
                    It.IsAny<CancellationToken>()
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
        public void Instance_WhenUninitialized_CreatesAndCachesDefaultAgent()
        {
            _agent.Dispose();

            var instanceField = typeof(Agent).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(instanceField, Is.Not.Null);

            instanceField!.SetValue(null, null);

            try
            {
                var first = Agent.Instance;
                var second = Agent.Instance;

                Assert.That(first, Is.Not.Null);
                Assert.That(second, Is.SameAs(first));
            }
            finally
            {
                if (instanceField.GetValue(null) is Agent instance)
                {
                    instance.Dispose();
                }

                instanceField.SetValue(null, null);
                _agent = new Agent(_zenApiMock.Object);
            }
        }

        [Test]
        public void NewInstance_DisposesPreviousInstance()
        {
            var instanceField = typeof(Agent).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            var cancellationSourceField = typeof(Agent).GetField("_cancellationSource", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(instanceField, Is.Not.Null);
            Assert.That(cancellationSourceField, Is.Not.Null);

            var first = Agent.NewInstance(_zenApiMock.Object);
            var firstCancellationSource = cancellationSourceField!.GetValue(first) as CancellationTokenSource;
            Assert.That(firstCancellationSource, Is.Not.Null);

            var second = Agent.NewInstance(ZenApiMock.CreateMock().Object);

            try
            {
                Assert.That(firstCancellationSource!.IsCancellationRequested, Is.True);
                Assert.That(Agent.Instance, Is.SameAs(second));
            }
            finally
            {
                second.Dispose();
                instanceField!.SetValue(null, null);
                _agent = new Agent(_zenApiMock.Object);
            }
        }

        [Test]
        public void NewInstance_DisposesPreviousInstanceBeforePublishingReplacement()
        {
            var instanceField = typeof(Agent).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            var cancellationSourceField = typeof(Agent).GetField("_cancellationSource", BindingFlags.NonPublic | BindingFlags.Instance);
            var backgroundTaskField = typeof(Agent).GetField("_backgroundTask", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(instanceField, Is.Not.Null);
            Assert.That(cancellationSourceField, Is.Not.Null);
            Assert.That(backgroundTaskField, Is.Not.Null);

            var first = Agent.NewInstance(_zenApiMock.Object);
            var firstCancellationSource = cancellationSourceField!.GetValue(first) as CancellationTokenSource;
            var firstBackgroundTask = backgroundTaskField!.GetValue(first) as Task;
            Assert.That(firstCancellationSource, Is.Not.Null);
            Assert.That(firstBackgroundTask, Is.Not.Null);

            firstCancellationSource!.Cancel();
            Assert.That(firstBackgroundTask!.Wait(TimeSpan.FromSeconds(5)), Is.True);

            Agent? instanceSeenByOldCallback = null;
            first.QueueEvent("test-token", Started.Create(), (_, _) =>
            {
                instanceSeenByOldCallback = Agent.Instance;
            });

            var second = Agent.NewInstance(ZenApiMock.CreateMock().Object);

            try
            {
                Assert.That(instanceSeenByOldCallback, Is.SameAs(first));
                Assert.That(Agent.Instance, Is.SameAs(second));
            }
            finally
            {
                second.Dispose();
                instanceField!.SetValue(null, null);
                _agent = new Agent(_zenApiMock.Object);
            }
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
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Test]
        public async Task Start_FetchesAndAppliesFirewallLists()
        {
            var blockedIpList = new FirewallListsAPIResponse.IPList
            {
                Key = "known_threat_actors/public_scanners",
                Ips = new[] { "203.0.113.10" }
            };
            var reportingApiMock = new Mock<IReportingAPIClient>();
            reportingApiMock
                .Setup(r => r.ReportAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReportingAPIResponse { Success = true });
            reportingApiMock
                .Setup(r => r.GetFirewallLists(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FirewallListsAPIResponse
                {
                    Success = true,
                    BlockedIPAddresses = new[] { blockedIpList }
                });

            _zenApiMock = ZenApiMock.CreateMock(reporting: reportingApiMock.Object);
            _agent = new Agent(_zenApiMock.Object);

            _agent.Start();

            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!_agent.Context.Config.GetMatchingBlockedIPListKeys("203.0.113.10").Any() && DateTime.UtcNow < deadline)
            {
                await Task.Delay(25);
            }

            Assert.That(
                _agent.Context.Config.GetMatchingBlockedIPListKeys("203.0.113.10"),
                Is.EquivalentTo(new[] { "known_threat_actors/public_scanners" })
            );
            reportingApiMock.Verify(
                r => r.GetFirewallLists(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Once
            );
        }

        [Test]
        public async Task Dispose_CancelsStartupConfigUpdate()
        {
            var startupConfigEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var startupConfigCanceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var reportingApiMock = new Mock<IReportingAPIClient>();
            reportingApiMock
                .Setup(r => r.ReportAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReportingAPIResponse { Success = true, Block = false });
            reportingApiMock
                .Setup(r => r.GetFirewallLists(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns<string, CancellationToken>((_, cancellationToken) =>
                {
                    startupConfigEntered.SetResult(true);
                    var response = new TaskCompletionSource<FirewallListsAPIResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
                    cancellationToken.Register(() =>
                    {
                        startupConfigCanceled.TrySetResult(true);
                        response.TrySetResult(new FirewallListsAPIResponse { Success = false, Error = "cancelled" });
                    });
                    return response.Task;
                });

            _zenApiMock = ZenApiMock.CreateMock(reporting: reportingApiMock.Object);
            _agent = new Agent(_zenApiMock.Object);

            _agent.Start();
            await startupConfigEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var disposeTask = Task.Run(() => _agent.Dispose());
            await startupConfigCanceled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await disposeTask.WaitAsync(TimeSpan.FromSeconds(5));
        }

        [Test]
        public void Start_HeartbeatFailureCallback_LogsWarning()
        {
            var loggerMock = new Mock<ILogger>();
            Agent.ConfigureLogger(loggerMock.Object);

            try
            {
                _agent.Start();

                var scheduledEventsField = typeof(Agent).GetField("_scheduledEvents", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.That(scheduledEventsField, Is.Not.Null);

                var scheduledEvents = scheduledEventsField!.GetValue(_agent) as ConcurrentDictionary<string, Agent.ScheduledItem>;
                Assert.That(scheduledEvents, Is.Not.Null);
                Assert.That(scheduledEvents!.TryGetValue(Heartbeat.ScheduleId, out var scheduledItem), Is.True);
                var callback = scheduledItem!.Callback;
                Assert.That(callback, Is.Not.Null);

                callback!(new Heartbeat(), new ReportingAPIResponse { Success = false, Error = "timeout" });

                loggerMock.Verify(logger => logger.Log(
                    It.Is<LogLevel>(level => level == LogLevel.Warning),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Heartbeat was not sent successfully: timeout")),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
            }
            finally
            {
                Agent.ConfigureLogger(null);
            }
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
                    It.IsAny<CancellationToken>()
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
                    It.IsAny<CancellationToken>()
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
                    It.IsAny<CancellationToken>()
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
                Url = "http://test.com/test",
                Method = "GET",
                RemoteAddress = "127.0.0.1"
            };

            // Act
            _agent.CaptureRequestUser(context);
            _agent.IncrementTotalRequestCount();

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
                Url = "http://test.com/test/path",
                Method = "POST",
                RemoteAddress = "192.168.1.1"
            };

            // Act
            _agent.CaptureRequestUser(context);
            _agent.IncrementTotalRequestCount();

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
        public async Task SendAttackEvent_WithNullContext_QueuesAttackWithoutRequest()
        {
            _agent.SendAttackEvent(AttackKind.StoredSsrf, null, null, "operation", null, "module", null, true, Array.Empty<string>());
            await Task.Delay(150);

            _zenApiMock.Verify(
                r => r.Reporting.ReportAsync(
                    It.IsAny<string>(),
                    It.Is<DetectedAttack>(a =>
                        a.Attack.Kind == "stored_ssrf" &&
                        a.Attack.Source == null &&
                        a.Request == null),
                    It.IsAny<CancellationToken>()),
                Times.Once);
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
                Headers = new Dictionary<string, string>
                {
                    { "Content-Type", "application/json" }
                }
            };
            var module = "authentication";
            var metadata = new Dictionary<string, string>
            {
                { "sql", payload }
            };
            var blocked = true;
            var paths = new[] { ".username" };
            _zenApiMock = ZenApiMock.CreateMock();
            _agent = new Agent(_zenApiMock.Object);

            // Act
            _agent.SendAttackEvent(kind, source, payload, operation, context, module, metadata, blocked, paths);
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
                        a.Attack.Path == paths[0] &&
                        a.Request.Url == context.Url &&
                        a.Request.Method == context.Method &&
                        a.Request.Headers.ContainsKey("Content-Type") &&
                        a.Attack.Metadata.ContainsKey("sql")

                    ),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Test]
        public async Task SendNonBlockingAttackEvent_UpdatesAttackDetectedAndNotBlocked()
        {
            var kind = AttackKind.SqlInjection;
            var source = Source.Body;
            var payload = "malicious-input";
            var operation = "login";
            var context = new Context
            {
                Url = "http://test.com/login",
                Method = "POST",
                Headers = new Dictionary<string, string>
                {
                    { "Content-Type", "application/json" }
                }
            };
            var module = "authentication";
            var metadata = new Dictionary<string, string>
            {
                { "sql", payload }
            };
            var blocked = false;
            _zenApiMock = ZenApiMock.CreateMock();
            _agent = new Agent(_zenApiMock.Object);

            // Act
            _agent.SendAttackEvent(kind, source, payload, operation, context, module, metadata, blocked, Array.Empty<string>());
            await Task.Delay(150);

            // Assert
            Assert.That(_agent.Context.AttacksDetected, Is.EqualTo(1));
            Assert.That(_agent.Context.AttacksBlocked, Is.EqualTo(0));
        }

        [Test]
        public async Task SendBlockingAttackEvent_UpdatesAttackDetectedAndBlocked()
        {
            var kind = AttackKind.SqlInjection;
            var source = Source.Body;
            var payload = "malicious-input";
            var operation = "login";
            var context = new Context
            {
                Url = "http://test.com/login",
                Method = "POST",
                Headers = new Dictionary<string, string>
                {
                    { "Content-Type", "application/json" }
                }
            };
            var module = "authentication";
            var metadata = new Dictionary<string, string>
            {
                { "sql", payload }
            };
            var blocked = true;
            _zenApiMock = ZenApiMock.CreateMock();
            _agent = new Agent(_zenApiMock.Object);

            // Act
            _agent.SendAttackEvent(kind, source, payload, operation, context, module, metadata, blocked, Array.Empty<string>());
            await Task.Delay(150);

            // Assert
            Assert.That(_agent.Context.AttacksDetected, Is.EqualTo(1));
            Assert.That(_agent.Context.AttacksBlocked, Is.EqualTo(1));
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
                r => r.Reporting.ReportAsync(It.IsAny<string>(), It.IsAny<IEvent>(), It.IsAny<CancellationToken>()),
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
                Url = "http://test.com/test",
                Route = "/test",
                Method = "GET",
                RemoteAddress = "1.2.3.4",
                UserAgent = "GoogleBot/2.1"
            };
            var monitoredIpList = new FirewallListsAPIResponse.IPList
            {
                Key = "tor/exit_nodes",
                Ips = new[] { "1.2.3.4/32" }
            };
            var userAgentDetail = new FirewallListsAPIResponse.UserAgentDetail
            {
                Key = "googlebot",
                Pattern = "googlebot"
            };

            _agent.Context.Config.UpdateFirewallLists(new FirewallListsAPIResponse
            {
                MonitoredIPAddresses = new[] { monitoredIpList },
                MonitoredUserAgents = "googlebot",
                UserAgentDetails = new[] { userAgentDetail }
            });
            _agent.Context.AddHostname("test.com");
            _agent.Context.AddUser(context.User, context.RemoteAddress);
            _agent.Context.AddRoute(context);
            _agent.Context.AddRequest();
            _agent.Context.AddRateLimitedRequest();
            _agent.Context.AddAttackDetected(true);
            _agent.Context.IsBlocked(context, out _);
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
                Assert.That(heartbeat.Stats.Requests.RateLimited, Is.EqualTo(1));
                Assert.That(heartbeat.Stats.Requests.AttacksDetected.Blocked, Is.EqualTo(1));
                Assert.That(heartbeat.Stats.Requests.AttacksDetected.Total, Is.EqualTo(1));
                Assert.That(heartbeat.Stats.StartedAt, Is.GreaterThan(0));
                Assert.That(heartbeat.Stats.EndedAt, Is.GreaterThan(heartbeat.Stats.StartedAt));
                Assert.That(heartbeat.Stats.IpAddresses.Breakdown["tor/exit_nodes"], Is.EqualTo(1));
                Assert.That(heartbeat.Stats.UserAgents.Breakdown["googlebot"], Is.EqualTo(1));
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
        public void ConfigChanged_WhenRemoteConfigVersionIsNewer_FetchesConfigAndReturnsTrue()
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

            var configVersionResponse = new ConfigLastUpdatedAPIResponse
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
            runtimeApiClientMock.Setup(x => x.GetConfigLastUpdated(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(configVersionResponse);
            runtimeApiClientMock.Setup(x => x.GetConfig(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(configResponse);
            _zenApiMock = ZenApiMock.CreateMock(runtime: runtimeApiClientMock.Object);
            _agent = new Agent(_zenApiMock.Object);
            _agent.Context.Config.ConfigLastUpdated = configLastUpdated;

            // Act
            var result = _agent.ConfigChanged(out var response);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(response.Success, Is.True);
                Assert.That(response.ConfigUpdatedAt, Is.EqualTo(newConfigLastUpdated));
                Assert.That(response.BlockedUserIds, Is.EquivalentTo(blockedUsers));
                Assert.That(response.Endpoints, Is.EquivalentTo(endpoints));
                Assert.That(_agent.Context.Config.ConfigLastUpdated, Is.EqualTo(configLastUpdated));
            });

            _zenApiMock.Verify(x => x.Runtime.GetConfigLastUpdated(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            _zenApiMock.Verify(x => x.Runtime.GetConfig(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestCase(123L, 123L)]
        [TestCase(124L, 123L)]
        public void ConfigChanged_WhenRemoteConfigVersionIsNotNewer_ReturnsFalse(long localConfigLastUpdated, long remoteConfigLastUpdated)
        {
            // Arrange
            var configVersionResponse = new ConfigLastUpdatedAPIResponse
            {
                Success = true,
                ConfigUpdatedAt = remoteConfigLastUpdated,
            };
            var runtimeApiClientMock = new Mock<IRuntimeAPIClient>();
            runtimeApiClientMock.Setup(x => x.GetConfigLastUpdated(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(configVersionResponse);
            _zenApiMock = ZenApiMock.CreateMock(runtime: runtimeApiClientMock.Object);
            _agent = new Agent(_zenApiMock.Object);
            _agent.Context.Config.ConfigLastUpdated = localConfigLastUpdated;

            // Act
            var result = _agent.ConfigChanged(out var response);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(response.Success, Is.True);
                Assert.That(response.ConfigUpdatedAt, Is.EqualTo(remoteConfigLastUpdated));
            });

            _zenApiMock.Verify(x => x.Runtime.GetConfigLastUpdated(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            _zenApiMock.Verify(x => x.Runtime.GetConfig(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public void ConfigChanged_WhenConfigFetchFails_ReturnsFalse()
        {
            // Arrange
            var configLastUpdated = 123L;
            var newConfigLastUpdated = 124L;

            var configVersionResponse = new ConfigLastUpdatedAPIResponse
            {
                Success = true,
                ConfigUpdatedAt = newConfigLastUpdated
            };

            var failedConfigResponse = new ReportingAPIResponse
            {
                Success = false,
                Error = "timeout"
            };

            var runtimeApiClientMock = new Mock<IRuntimeAPIClient>();
            runtimeApiClientMock.Setup(x => x.GetConfigLastUpdated(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(configVersionResponse);
            runtimeApiClientMock.Setup(x => x.GetConfig(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(failedConfigResponse);
            _zenApiMock = ZenApiMock.CreateMock(runtime: runtimeApiClientMock.Object);
            _agent = new Agent(_zenApiMock.Object);
            _agent.Context.Config.ConfigLastUpdated = configLastUpdated;

            // Act
            var result = _agent.ConfigChanged(out var response);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(response.Success, Is.False);
                Assert.That(_agent.Context.Config.ConfigLastUpdated, Is.EqualTo(configLastUpdated));
            });

            _zenApiMock.Verify(x => x.Runtime.GetConfigLastUpdated(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            _zenApiMock.Verify(x => x.Runtime.GetConfig(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task RecurringTasks_WhenRemoteConfigIsNewer_UpdatesConfigAndFirewallLists()
        {
            // Arrange
            var runtimeApiClientMock = new Mock<IRuntimeAPIClient>();
            runtimeApiClientMock
                .Setup(x => x.GetConfigLastUpdated(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ConfigLastUpdatedAPIResponse
                {
                    Success = true,
                    ConfigUpdatedAt = 200
                });
            runtimeApiClientMock
                .Setup(x => x.GetConfig(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReportingAPIResponse
                {
                    Success = true,
                    Endpoints = Array.Empty<EndpointConfig>(),
                    ConfigUpdatedAt = 200
                });

            var reportingApiClientMock = new Mock<IReportingAPIClient>();
            reportingApiClientMock
                .Setup(x => x.GetFirewallLists(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FirewallListsAPIResponse
                {
                    Success = true,
                    BlockedIPAddresses = new[]
                    {
                        new FirewallListsAPIResponse.IPList
                        {
                            Key = "recurring-test-list",
                            Ips = new[] { "203.0.113.20" }
                        }
                    }
                });

            _agent.Dispose();
            _zenApiMock = ZenApiMock.CreateMock(
                reporting: reportingApiClientMock.Object,
                runtime: runtimeApiClientMock.Object);
            _agent = new Agent(_zenApiMock.Object);

            var lastConfigCheckField = typeof(Agent).GetField("_lastConfigCheck", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(lastConfigCheckField, Is.Not.Null);
            lastConfigCheckField!.SetValue(_agent, DateTime.UtcNow.AddMinutes(-2).Ticks);

            // Act
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while ((_agent.Context.Config.ConfigLastUpdated != 200 ||
                    !_agent.Context.Config.GetMatchingBlockedIPListKeys("203.0.113.20").Any()) &&
                   DateTime.UtcNow < deadline)
            {
                await Task.Delay(25);
            }

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(_agent.Context.Config.ConfigLastUpdated, Is.EqualTo(200));
                Assert.That(
                    _agent.Context.Config.GetMatchingBlockedIPListKeys("203.0.113.20"),
                    Is.EquivalentTo(new[] { "recurring-test-list" }));
            });
        }

        [Test]
        public async Task UpdateFirewallLists_WithEmptyToken_DoesNotFetchLists()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", "");

            // Act
            await _agent.UpdateFirewallLists();

            // Assert
            _zenApiMock.Verify(x => x.Reporting.GetFirewallLists(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public void AddRoute_AddsRouteToContext()
        {
            // Arrange
            var context = new Context
            {
                Url = "http://test.com/test/route",
                Method = "GET",
                RemoteAddress = "192.168.1.1",
                Route = "/test/route"
            };

            // Act
            _agent.AddRoute(context);

            // Assert
            Assert.That(_agent.Context.Routes.Count, Is.EqualTo(1));
            Assert.That(_agent.Context.Routes.Any(r => r.Path == "/test/route"));
        }
    }
}
