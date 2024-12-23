using Aikido.Zen.Core;
using Aikido.Zen.Core.Patches;
using NUnit.Framework;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Aikido.Zen.Test
{
    public class HttpClientPatchTests
    {
        private HttpClient _httpClient;
        private Agent _originalAgent;

        [SetUp]
        public void Setup()
        {
            _httpClient = new HttpClient();
            _originalAgent = Agent.Instance;
        }

        [TearDown]
        public void TearDown()
        {
            _httpClient.Dispose();
            Agent.Instance = _originalAgent;
        }

        [Test]
        public void CaptureRequest_WithNullBaseAddress_UsesRequestUri()
        {
            // Arrange
            var agentMock = new Agent(new Mock<IZenApi>().Object);
            Agent.Instance = agentMock;
            var request = new HttpRequestMessage(HttpMethod.Get, "http://test.com:8080/path");

            // Act
            var result = HttpClientPatch.CaptureRequest(request, CancellationToken.None, _httpClient);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(agentMock.Context.Hostnames.Count, Is.EqualTo(1));
            var hostname = agentMock.Context.Hostnames[0];
            Assert.Multiple(() =>
            {
                Assert.That(hostname.Hostname, Is.EqualTo("test.com"));
                Assert.That(hostname.Port, Is.EqualTo(8080));
            });
        }

        [Test]
        public void CaptureRequest_WithBaseAddressAndNullRequestUri_UsesBaseAddress()
        {
            // Arrange
            var agentMock = new Agent(new Mock<IZenApi>().Object);
            Agent.Instance = agentMock;
            _httpClient.BaseAddress = new Uri("http://example.com:9090/");
            var request = new HttpRequestMessage();

            // Act
            var result = HttpClientPatch.CaptureRequest(request, CancellationToken.None, _httpClient);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(agentMock.Context.Hostnames.Count, Is.EqualTo(1));
            var hostname = agentMock.Context.Hostnames[0];
            Assert.Multiple(() =>
            {
                Assert.That(hostname.Hostname, Is.EqualTo("example.com"));
                Assert.That(hostname.Port, Is.EqualTo(9090));
            });
        }

        [Test]
        public void CaptureRequest_WithBaseAddressAndRequestUri_CombinesUris()
        {
            // Arrange
            var agentMock = new Agent(new Mock<IZenApi>().Object);
            Agent.Instance = agentMock;
            _httpClient.BaseAddress = new Uri("http://base.com:8080/");
            var request = new HttpRequestMessage(HttpMethod.Get, "api/endpoint");

            // Act
            var result = HttpClientPatch.CaptureRequest(request, CancellationToken.None, _httpClient);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(agentMock.Context.Hostnames.Count, Is.EqualTo(1));
            var hostname = agentMock.Context.Hostnames[0];
            Assert.Multiple(() =>
            {
                Assert.That(hostname.Hostname, Is.EqualTo("base.com"));
                Assert.That(hostname.Port, Is.EqualTo(8080));
            });
        }

        [Test]
        public void CaptureRequest_WithAikidoDevHostname_SkipsCapture()
        {
            // Arrange
            var agentMock = new Agent(new Mock<IZenApi>().Object);
            Agent.Instance = agentMock;
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.aikido.dev/endpoint");

            // Act
            var result = HttpClientPatch.CaptureRequest(request, CancellationToken.None, _httpClient);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(agentMock.Context.Hostnames.Count, Is.EqualTo(0));
        }
    }
}
