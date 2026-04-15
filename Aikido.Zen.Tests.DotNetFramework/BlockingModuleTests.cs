using System;
using System.IO;
using System.Linq;
using System.Web;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Models;
using Aikido.Zen.DotNetFramework;
using Aikido.Zen.DotNetFramework.HttpModules;
using NUnit.Framework;

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
            Agent.Instance.Context.UpdateBlockedUsers(new[] { "blocked-user" });

            try
            {
                var user = new User("blocked-user", "Blocked User");
                var output = new StringWriter();
                var httpContext = new HttpContext(
                    new HttpRequest(string.Empty, "http://test.local/api/test", string.Empty),
                    new HttpResponse(output));
                var aikidoContext = new Context
                {
                    Url = "http://test.local/api/test",
                    Path = "/api/test",
                    Method = "GET",
                    Route = "/api/test",
                    RemoteAddress = "127.0.0.1",
                    User = user
                };
                httpContext.Items["Aikido.Zen.Context"] = aikidoContext;
                var completed = false;

                BlockingModule.HandleBlocking(httpContext, () => completed = true);

                var capturedUser = Agent.Instance.Context.Users.SingleOrDefault(u => u.Id == user.Id);

                Assert.Multiple(() =>
                {
                    Assert.That(capturedUser, Is.Not.Null);
                    Assert.That(capturedUser.Name, Is.EqualTo(user.Name));
                    Assert.That(capturedUser.LastIpAddress, Is.EqualTo("127.0.0.1"));
                    Assert.That(httpContext.Response.StatusCode, Is.EqualTo(403));
                    Assert.That(output.ToString(), Is.EqualTo("Your request is blocked: User is blocked"));
                    Assert.That(completed, Is.True);
                });
            }
            finally
            {
                Agent.Instance.ClearContext();
                Agent.Instance.Context.UpdateBlockedUsers(System.Array.Empty<string>());
            }
        }

        [Test]
        public void GetUser_ReturnsContextUser()
        {
            var originalCurrent = HttpContext.Current;

            try
            {
                var user = new User("context-user", "Context User");
                var httpContext = new HttpContext(
                    new HttpRequest(string.Empty, "http://test.local/api/test", string.Empty),
                    new HttpResponse(new StringWriter()));
                HttpContext.Current = httpContext;
                httpContext.Items["Aikido.Zen.Context"] = new Context
                {
                    Url = "http://test.local/api/test",
                    Path = "/api/test",
                    Method = "GET",
                    Route = "/api/test",
                    RemoteAddress = "127.0.0.1",
                    User = user
                };

                Assert.That(Aikido.Zen.DotNetFramework.Zen.GetUser(), Is.SameAs(user));
            }
            finally
            {
                HttpContext.Current = originalCurrent;
            }
        }
    }
}
