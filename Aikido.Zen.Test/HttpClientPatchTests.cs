using System;
using System.Net.Http;
using System.Threading.Tasks;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Patches;
using Aikido.Zen.Tests.Mocks;
using NUnit.Framework;

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

        private Context CreateContext()
        {
            // Create a new Context for each test
            return new Context();
        }

        [Test]
        public void OnHttpClient_WithNullBaseAddress_UsesRequestUri()
        {
            // Arrange
            Agent.NewInstance(ZenApiMock.CreateMock().Object);
            Agent.Instance.ClearContext();
            var request = new HttpRequestMessage(HttpMethod.Get, "http://test.com:8080/path");
            var context = CreateContext();

            // Act
            var result = HttpClientPatcher.OnHttpClient(request, _httpClient, null, context);

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
        public async Task OnHttpClient_WithBaseAddressAndNullRequestUri_UsesBaseAddress()
        {
            // Arrange
            Agent.NewInstance(ZenApiMock.CreateMock().Object);
            _httpClient.BaseAddress = new Uri("http://example.com:9090/");
            var request = new HttpRequestMessage();
            var context = CreateContext();

            // Act
            var result = HttpClientPatcher.OnHttpClient(request, _httpClient, null, context);
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
        public async Task OnHttpClient_WithBaseAddressAndRequestUri_CombinesUris()
        {
            // Arrange
            Agent.NewInstance(ZenApiMock.CreateMock().Object);
            Agent.Instance.ClearContext();
            _httpClient.BaseAddress = new Uri("http://base.com:8080/");
            var request = new HttpRequestMessage(HttpMethod.Get, "api/endpoint");
            var context = CreateContext();

            // Act
            var result = HttpClientPatcher.OnHttpClient(request, _httpClient, null, context);
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
        public void OnHttpClient_WithAikidoDevHostname_SkipsCapture()
        {
            // Arrange
            Agent.NewInstance(ZenApiMock.CreateMock().Object);
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.aikido.dev/endpoint");
            var context = CreateContext();

            // Act
            var result = HttpClientPatcher.OnHttpClient(request, _httpClient, null, context);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(Agent.Instance.Context.Hostnames.Count, Is.EqualTo(0));
        }
    }
}
