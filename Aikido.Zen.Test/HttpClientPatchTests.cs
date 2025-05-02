using Aikido.Zen.Core;
using Aikido.Zen.Core.Patches;
using Aikido.Zen.Tests.Mocks;

namespace Aikido.Zen.Test
{
    public class HttpClientPatchTests
    {
        private HttpClient _httpClient;

        [SetUp]
        public void Setup()
        {
            _httpClient = new HttpClient();
        }

        [TearDown]
        public void TearDown()
        {
            _httpClient.Dispose();
        }

        [Test]
        public void CaptureRequest_WithNullBaseAddress_UsesRequestUri()
        {
            // Arrange
            Agent.NewInstance(ZenApiMock.CreateMock().Object);
            Agent.Instance.ClearContext();
            var request = new HttpRequestMessage(HttpMethod.Get, "http://test.com:8080/path");

            // Act
            var result = HttpClientPatches.CaptureRequest(request, _httpClient, null);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(Agent.Instance.Context.Hostnames.Count, Is.EqualTo(1));
            var hostname = Agent.Instance.Context.Hostnames.FirstOrDefault();
            Assert.Multiple(() =>
            {
                Assert.That(hostname.Hostname, Is.EqualTo("test.com"));
                Assert.That(hostname.Port, Is.EqualTo(8080));
            });
        }

        [Test]
        public async Task CaptureRequest_WithBaseAddressAndNullRequestUri_UsesBaseAddress()
        {
            // Arrange
            Agent.NewInstance(ZenApiMock.CreateMock().Object);
            _httpClient.BaseAddress = new Uri("http://example.com:9090/");
            var request = new HttpRequestMessage();

            // Act
            var result = HttpClientPatches.CaptureRequest(request, _httpClient, null);
            await Task.Delay(100);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(Agent.Instance.Context.Hostnames.Count, Is.EqualTo(1));
            var hostname = Agent.Instance.Context.Hostnames.FirstOrDefault();
            Assert.Multiple(() =>
            {
                Assert.That(hostname.Hostname, Is.EqualTo("example.com"));
                Assert.That(hostname.Port, Is.EqualTo(9090));
            });
        }

        [Test]
        public async Task CaptureRequest_WithBaseAddressAndRequestUri_CombinesUris()
        {
            // Arrange
            Agent.NewInstance(ZenApiMock.CreateMock().Object);
            Agent.Instance.ClearContext();
            _httpClient.BaseAddress = new Uri("http://base.com:8080/");
            var request = new HttpRequestMessage(HttpMethod.Get, "api/endpoint");

            // Act
            var result = HttpClientPatches.CaptureRequest(request, _httpClient, null);
            await Task.Delay(100);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(Agent.Instance.Context.Hostnames.Count, Is.EqualTo(1));
            var hostname = Agent.Instance.Context.Hostnames.FirstOrDefault();
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
            Agent.NewInstance(ZenApiMock.CreateMock().Object);
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.aikido.dev/endpoint");

            // Act
            var result = HttpClientPatches.CaptureRequest(request, _httpClient, null);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(Agent.Instance.Context.Hostnames.Count, Is.EqualTo(0));
        }
    }
}
