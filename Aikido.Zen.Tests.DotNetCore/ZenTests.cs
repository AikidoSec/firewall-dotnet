using System.Net;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Models;
using Aikido.Zen.DotNetCore.Middleware;
using Microsoft.AspNetCore.Http;
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
