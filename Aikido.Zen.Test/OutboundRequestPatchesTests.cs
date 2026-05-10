using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Sinks;
using Aikido.Zen.Tests.Mocks;
using Moq;

namespace Aikido.Zen.Test
{
    [TestFixture]
    public class OutboundRequestPatchesTests
    {
        private Context _context = null!;
        private Agent _agent = null!;

        [SetUp]
        public void SetUp()
        {
            Patcher.Unpatch();
            _context = new Context
            {
                ParsedUserInput = new Dictionary<string, string>()
            };

            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", "test-token");
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            Environment.SetEnvironmentVariable("AIKIDO_URL", "http://localhost:3000");
            Environment.SetEnvironmentVariable("AIKIDO_REALTIME_URL", "http://localhost:3001");

            var reportingApiMock = new Mock<IReportingAPIClient>();
            reportingApiMock
                .Setup(r => r.ReportAsync(It.IsAny<string>(), It.IsAny<object>()))
                .ReturnsAsync(new ReportingAPIResponse { Success = true });
            reportingApiMock
                .Setup(r => r.GetFirewallLists(It.IsAny<string>()))
                .ReturnsAsync(new FirewallListsAPIResponse { Success = true });

            var runtimeApiMock = new Mock<IRuntimeAPIClient>();
            runtimeApiMock
                .Setup(r => r.GetConfig(It.IsAny<string>()))
                .ReturnsAsync(new ReportingAPIResponse { Success = true });
            runtimeApiMock
                .Setup(r => r.GetConfigLastUpdated(It.IsAny<string>()))
                .ReturnsAsync(new ConfigLastUpdatedAPIResponse { Success = true });

            _agent = Agent.NewInstance(ZenApiMock.CreateMock(reportingApiMock.Object, runtimeApiMock.Object).Object);
            _agent.ClearContext();
            Patcher.PatchSinks(() => _context);
        }

        [TearDown]
        public void TearDown()
        {
            Patcher.Unpatch();
            _agent?.Dispose();

            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", null);
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", null);
            Environment.SetEnvironmentVariable("AIKIDO_URL", null);
            Environment.SetEnvironmentVariable("AIKIDO_REALTIME_URL", null);
        }

        [Test]
        public void PatchMethods_ForwardHttpClientAndWebRequestArgumentsToSink()
        {
            var method = GetMethod(
                typeof(HttpClient),
                nameof(HttpClient.SendAsync),
                typeof(HttpRequestMessage),
                typeof(CancellationToken));

            using (var httpClient = new HttpClient())
            using (var request = new HttpRequestMessage(HttpMethod.Get, "https://safe.example/path"))
            {
                Assert.That(OutboundRequestPatches.HttpClientRequest(request, httpClient, method), Is.True);
            }

            using (var httpClient = new HttpClient { BaseAddress = new Uri("https://base.example") })
            using (var request = new HttpRequestMessage(HttpMethod.Get, "/relative"))
            {
                Assert.That(OutboundRequestPatches.HttpClientRequest(request, httpClient, method), Is.True);
            }

            using (var httpClient = new HttpClient { BaseAddress = new Uri("https://base-only.example") })
            {
                Assert.That(OutboundRequestPatches.HttpClientRequest(null!, httpClient, method), Is.True);
            }

            using (var request = new HttpRequestMessage(HttpMethod.Get, "https://safe.example/path"))
            {
                Assert.That(OutboundRequestPatches.HttpClientRequest(request, null!, method), Is.True);
            }

            Assert.That(OutboundRequestPatches.HttpClientRequest(null!, null!, method), Is.True);

#pragma warning disable SYSLIB0014
            var webRequest = WebRequest.Create("https://safe.example/path");
#pragma warning restore SYSLIB0014
            Assert.That(OutboundRequestPatches.WebRequest(webRequest, GetMethod(typeof(WebRequest), nameof(WebRequest.GetResponse))), Is.True);
            Assert.That(OutboundRequestPatches.WebRequest(null!, GetMethod(typeof(WebRequest), nameof(WebRequest.GetResponse))), Is.True);
        }

        private static MethodInfo GetMethod(Type type, string methodName, params Type[] parameterTypes)
        {
            var method = type.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static,
                null,
                parameterTypes,
                null);
            Assert.That(method, Is.Not.Null, $"{type.FullName}.{methodName} should exist.");
            return method;
        }
    }
}
