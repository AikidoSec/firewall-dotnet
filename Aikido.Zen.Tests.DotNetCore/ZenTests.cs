using System.Collections.Concurrent;
using System.Net;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Models.Events;
using Aikido.Zen.DotNetCore;
using Aikido.Zen.DotNetCore.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ZenApi = Aikido.Zen.DotNetCore.Zen;

namespace Aikido.Zen.Tests.DotNetCore
{
    public class ZenTests
    {
        private Mock<ILogger> _loggerMock;

        [SetUp]
        public void SetUp()
        {
            _loggerMock = new Mock<ILogger>();
            Agent.ConfigureLogger(_loggerMock.Object);
            Agent.Instance.Context.Config.Clear();
            Agent.Instance.ClearContext();
        }

        [TearDown]
        public void TearDown()
        {
            Agent.Instance.ClearContext();
            Agent.Instance.Context.Config.Clear();
            Agent.ConfigureLogger(null);
        }

        [Test]
        public void SetUser_BeforeContextMiddleware_StoresUserWithoutCapturing()
        {
            var httpContext = new DefaultHttpContext();

            ZenApi.SetUser("user-123", "Test User", httpContext);

            var currentUser = httpContext.Items["Aikido.Zen.CurrentUser"] as User;

            Assert.Multiple(() =>
            {
                Assert.That(currentUser, Is.Not.Null);
                Assert.That(currentUser?.Id, Is.EqualTo("user-123"));
                Assert.That(Agent.Instance.Context.Users, Is.Empty);
            });

            _loggerMock.Verify(logger => logger.Log(
                It.Is<LogLevel>(level => level == LogLevel.Warning),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Never);
        }

        [Test]
        public async Task SetUser_AfterContextMiddleware_WarnsOnceAndCapturesFinalUser()
        {
            var bypassedHttpContext = new DefaultHttpContext();
            var bypassedContext = new Context { Bypassed = true };
            bypassedHttpContext.Items["Aikido.Zen.Context"] = bypassedContext;

            ZenApi.SetUser("bypassed-user", "Bypassed User", bypassedHttpContext);

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Scheme = "http";
            httpContext.Request.Host = new HostString("test.local");
            httpContext.Request.Path = "/api/test";
            httpContext.Request.Method = "GET";
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
            var contextMiddleware = new ContextMiddleware([]);

            await contextMiddleware.InvokeAsync(httpContext, requestContext =>
            {
                ZenApi.SetUser("user-123", "First User", requestContext);
                ZenApi.SetUser("user-456", "Second User", requestContext);
                Assert.That(Agent.Instance.Context.Users, Is.Empty);
                return Task.CompletedTask;
            });

            var aikidoContext = httpContext.Items["Aikido.Zen.Context"] as Context;
            var capturedUser = Agent.Instance.Context.Users.SingleOrDefault(user => user.Id == "user-456");

            _loggerMock.Verify(logger => logger.Log(
                It.Is<LogLevel>(level => level == LogLevel.Warning),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((value, _) => value.ToString()!.Contains("before UseZenFirewall()")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);

            Assert.Multiple(() =>
            {
                Assert.That(bypassedContext.User?.Id, Is.EqualTo("bypassed-user"));
                Assert.That(Agent.Instance.Context.Users.Any(user => user.Id == "bypassed-user"), Is.False);
                Assert.That(aikidoContext?.User?.Id, Is.EqualTo("user-456"));
                Assert.That(Agent.Instance.Context.Users.Any(user => user.Id == "user-123"), Is.False);
                Assert.That(capturedUser?.Hits, Is.EqualTo(1));
            });
        }

        [TestCase(false, "test-token", true)]
        [TestCase(true, "test-token", false)]
        [TestCase(false, null, false)]
        public async Task Track_ReportsFirst25EventsPerRequestOnlyWhenEnabled(bool disabled, string? token, bool shouldReport)
        {
            var originalDisable = Environment.GetEnvironmentVariable("AIKIDO_DISABLE");
            var originalToken = Environment.GetEnvironmentVariable("AIKIDO_TOKEN");
            var reportedEvents = new ConcurrentQueue<CustomEvent>();
            var reportingApi = new Mock<IReportingAPIClient>();
            reportingApi
                .Setup(api => api.ReportAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .Callback<string, object, CancellationToken>((_, evt, _) => reportedEvents.Enqueue((CustomEvent)evt))
                .ReturnsAsync(new ReportingAPIResponse { Success = true });
            var agent = Agent.NewInstance(new Aikido.Zen.Core.Api.ZenApi(reportingApi.Object, Mock.Of<IRuntimeAPIClient>()));
            var accessor = new HttpContextAccessor();
            using var services = new ServiceCollection()
                .AddSingleton<IHttpContextAccessor>(accessor)
                .AddSingleton(new ContextAccessor(accessor))
                .BuildServiceProvider();

            try
            {
                Environment.SetEnvironmentVariable("AIKIDO_TOKEN", token);
                ZenApi.Initialize(services, accessor);
                for (var requestIndex = 0; requestIndex < 2; requestIndex++)
                {
                    var httpContext = new DefaultHttpContext();
                    httpContext.Request.Scheme = "https";
                    httpContext.Request.Host = new HostString("test.local");
                    httpContext.Request.Path = "/login";
                    httpContext.Request.Method = "POST";
                    httpContext.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
                    accessor.HttpContext = httpContext;
                    Environment.SetEnvironmentVariable("AIKIDO_DISABLE", "false");
                    ZenApi.SetUser($"user-{requestIndex}", "Test User", httpContext);

                    await new ContextMiddleware([]).InvokeAsync(httpContext, _ =>
                    {
                        Environment.SetEnvironmentVariable("AIKIDO_DISABLE", disabled ? "true" : "false");
                        for (var eventIndex = 0; eventIndex < 30; eventIndex++)
                        {
                            ZenApi.Track($"request-{requestIndex}.event-{eventIndex}");
                        }
                        return Task.CompletedTask;
                    });
                }

                agent.Dispose();
                Assert.That(reportedEvents.Count, Is.EqualTo(shouldReport ? 50 : 0));
                if (shouldReport)
                {
                    for (var requestIndex = 0; requestIndex < 2; requestIndex++)
                    {
                        var requestEvents = reportedEvents.Where(evt => evt.User.Id == $"user-{requestIndex}").ToArray();
                        Assert.Multiple(() =>
                        {
                            Assert.That(requestEvents.Select(evt => evt.Name), Is.EquivalentTo(
                                Enumerable.Range(0, 25).Select(eventIndex => $"request-{requestIndex}.event-{eventIndex}")));
                            Assert.That(requestEvents.Select(evt => evt.Request.Method), Is.All.EqualTo("POST"));
                            Assert.That(requestEvents.Select(evt => evt.Request.IpAddress), Is.All.EqualTo("203.0.113.7"));
                            Assert.That(requestEvents.Select(evt => evt.Request.Route), Is.All.EqualTo("/login"));
                        });
                    }
                }
            }
            finally
            {
                agent.Dispose();
                ZenApi.Initialize(null, null);
                accessor.HttpContext = null;
                Environment.SetEnvironmentVariable("AIKIDO_DISABLE", originalDisable);
                Environment.SetEnvironmentVariable("AIKIDO_TOKEN", originalToken);
            }
        }

        [Test]
        public void Track_DoesNotThrowWhenTheConfiguredLoggerThrows()
        {
            var originalDisable = Environment.GetEnvironmentVariable("AIKIDO_DISABLE");
            var originalDebug = Environment.GetEnvironmentVariable("AIKIDO_DEBUG");
            var originalToken = Environment.GetEnvironmentVariable("AIKIDO_TOKEN");
            var throwingLogger = new Mock<ILogger>();
            throwingLogger
                .Setup(logger => logger.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
                .Throws(new InvalidOperationException("Logger failed"));

            try
            {
                Environment.SetEnvironmentVariable("AIKIDO_DISABLE", "false");
                Environment.SetEnvironmentVariable("AIKIDO_DEBUG", "true");
                Environment.SetEnvironmentVariable("AIKIDO_TOKEN", "test-token");
                Agent.ConfigureLogger(throwingLogger.Object);

                Assert.DoesNotThrow(() => ZenApi.Track(string.Empty));
                throwingLogger.Verify(logger => logger.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce());
            }
            finally
            {
                Environment.SetEnvironmentVariable("AIKIDO_DISABLE", originalDisable);
                Environment.SetEnvironmentVariable("AIKIDO_DEBUG", originalDebug);
                Environment.SetEnvironmentVariable("AIKIDO_TOKEN", originalToken);
                Agent.ConfigureLogger(_loggerMock.Object);
            }
        }

        [Test]
        public void SetRateLimitGroup_SetsGroupOnHttpContext()
        {
            var context = new DefaultHttpContext();

            ZenApi.SetRateLimitGroup("team-1", context);

            Assert.That(context.Items["Aikido.Zen.RateLimitGroup"], Is.EqualTo("team-1"));
        }

        [Test]
        public void SetRateLimitGroup_DoesNotSetEmptyGroup()
        {
            var context = new DefaultHttpContext();

            ZenApi.SetRateLimitGroup(string.Empty, context);

            Assert.That(context.Items.ContainsKey("Aikido.Zen.RateLimitGroup"), Is.False);
        }
    }
}
