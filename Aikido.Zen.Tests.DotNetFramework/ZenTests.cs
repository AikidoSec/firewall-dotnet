using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Models.Events;
using Moq;
using NUnit.Framework;
using FrameworkZen = Aikido.Zen.DotNetFramework.Zen;

namespace Aikido.Zen.Tests.DotNetFramework
{
    public class ZenTests
    {
        [TestCase(false, "test-token", true)]
        [TestCase(true, "test-token", false)]
        [TestCase(false, null, false)]
        public void Track_ReportsFirst25EventsPerRequestOnlyWhenEnabled(bool disabled, string token, bool shouldReport)
        {
            var originalDisable = Environment.GetEnvironmentVariable("AIKIDO_DISABLE");
            var originalToken = Environment.GetEnvironmentVariable("AIKIDO_TOKEN");
            var originalContext = HttpContext.Current;
            var reportedEvents = new ConcurrentQueue<CustomEvent>();
            var reportingApi = new Mock<IReportingAPIClient>();
            reportingApi
                .Setup(api => api.ReportAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .Callback<string, object, CancellationToken>((_, evt, cancellationToken) => reportedEvents.Enqueue((CustomEvent)evt))
                .ReturnsAsync(new ReportingAPIResponse { Success = true });
            var agent = Agent.NewInstance(new ZenApi(reportingApi.Object, Mock.Of<IRuntimeAPIClient>()));

            try
            {
                Environment.SetEnvironmentVariable("AIKIDO_DISABLE", disabled ? "true" : "false");
                Environment.SetEnvironmentVariable("AIKIDO_TOKEN", token);
                for (var requestIndex = 0; requestIndex < 2; requestIndex++)
                {
                    HttpContext.Current = new HttpContext(
                        new HttpRequest(string.Empty, "https://test.local/login", string.Empty),
                        new HttpResponse(new StringWriter()));
                    FrameworkZen.SetCurrentContext(new Context
                    {
                        Method = "POST",
                        RemoteAddress = "203.0.113.7",
                        Route = "/login",
                        User = new User($"user-{requestIndex}", "Test User")
                    });
                    for (var eventIndex = 0; eventIndex < 30; eventIndex++)
                    {
                        FrameworkZen.Track($"request-{requestIndex}.event-{eventIndex}");
                    }
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
                HttpContext.Current = originalContext;
                Environment.SetEnvironmentVariable("AIKIDO_DISABLE", originalDisable);
                Environment.SetEnvironmentVariable("AIKIDO_TOKEN", originalToken);
            }
        }

    }
}
