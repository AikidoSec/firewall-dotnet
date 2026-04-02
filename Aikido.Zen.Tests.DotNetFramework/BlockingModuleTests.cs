using System.IO;
using System.Web;
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
    }
}
