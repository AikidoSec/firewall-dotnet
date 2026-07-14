using System;
using System.IO;
using System.Web;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Models;
using Aikido.Zen.DotNetFramework;
using Aikido.Zen.DotNetFramework.HttpModules;
using NUnit.Framework;
using FrameworkZen = Aikido.Zen.DotNetFramework.Zen;

namespace Aikido.Zen.Tests.DotNetFramework
{
    public class BlockingModuleTests
    {
        [Test]
        public void CompleteRequestWithResponse_WritesForbiddenResponse_AndCompletesRequest()
        {
            var output = new StringWriter();
            var context = new HttpContext(
                new HttpRequest(string.Empty, "http://test.local/api/test", string.Empty),
                new HttpResponse(output));
            var completed = false;

            BlockingModule.CompleteRequestWithResponse(
                context,
                403,
                "Your request is blocked: User is blocked",
                () => completed = true);

            Assert.Multiple(() =>
            {
                Assert.That(context.Response.StatusCode, Is.EqualTo(403));
                Assert.That(output.ToString(), Is.EqualTo("Your request is blocked: User is blocked"));
                Assert.That(completed, Is.True);
            });
        }

        [Test]
        public void CompleteRequestWithResponse_WritesRateLimitedResponse_WhenRetryAfterIsProvided()
        {
            var output = new StringWriter();
            var context = new HttpContext(
                new HttpRequest(string.Empty, "http://test.local/api/test", string.Empty),
                new HttpResponse(output));
            var completed = false;

            BlockingModule.CompleteRequestWithResponse(
                context,
                429,
                "You are rate limited by Aikido firewall. (Your IP: 127.0.0.1)",
                () => completed = true,
                "60000");

            Assert.Multiple(() =>
            {
                Assert.That(context.Response.StatusCode, Is.EqualTo(429));
                Assert.That(output.ToString(), Is.EqualTo("You are rate limited by Aikido firewall. (Your IP: 127.0.0.1)"));
                Assert.That(completed, Is.True);
            });
        }

        [Test]
        public void HandleBlocking_BlocksRequest_WhenContextUserIsBlocked()
        {
            Agent.Instance.ClearContext();
            Agent.Instance.Context.Config.UpdateBlockedUsers(new[] { "blocked-user" });

            try
            {
                var user = new User("blocked-user", "Blocked User");
                var output = new StringWriter();
                var httpContext = new HttpContext(
                    new HttpRequest(string.Empty, "http://test.local/api/test", string.Empty),
                    new HttpResponse(output));
                HttpContext.Current = httpContext;
                var aikidoContext = new Context
                {
                    Url = "http://test.local/api/test",
                    Path = "/api/test",
                    Method = "GET",
                    Route = "/api/test",
                    RemoteAddress = "127.0.0.1",
                    User = user
                };
                FrameworkZen.SetCurrentContext(aikidoContext);
                var completed = false;

                BlockingModule.HandleBlocking(httpContext, () => completed = true);

                Assert.Multiple(() =>
                {
                    Assert.That(Agent.Instance.Context.Users, Is.Empty);
                    Assert.That(httpContext.Response.StatusCode, Is.EqualTo(403));
                    Assert.That(output.ToString(), Is.EqualTo("Your request is blocked: User is blocked"));
                    Assert.That(completed, Is.True);
                });
            }
            finally
            {
                FrameworkZen.ClearCurrentContext();
                HttpContext.Current = null;
                Agent.Instance.ClearContext();
                Agent.Instance.Context.Config.UpdateBlockedUsers(System.Array.Empty<string>());
            }
        }

        [Test]
        public void GetRetryAfterHeaderValue_ConvertsWindowMillisecondsToSeconds()
        {
            Assert.Multiple(() =>
            {
                Assert.That(BlockingModule.GetRetryAfterHeaderValue(new RateLimitingConfig { WindowSizeInMS = 60000 }), Is.EqualTo("60"));
                Assert.That(BlockingModule.GetRetryAfterHeaderValue(new RateLimitingConfig { WindowSizeInMS = 1500 }), Is.EqualTo("2"));
            });
        }

        [Test]
        public void GetUser_ReturnsContextUser()
        {
            try
            {
                var user = new User("context-user", "Context User");
                var context = new Context
                {
                    Url = "http://test.local/api/test",
                    Path = "/api/test",
                    Method = "GET",
                    Route = "/api/test",
                    RemoteAddress = "127.0.0.1",
                    User = user
                };
                FrameworkZen.SetCurrentContext(context);

                Assert.That(FrameworkZen.GetUser(), Is.SameAs(user));
            }
            finally
            {
                FrameworkZen.ClearCurrentContext();
            }
        }

    }
}
