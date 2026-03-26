using Aikido.Zen.Core.Api;
using System.Linq;
using System.Net.Http;

namespace Aikido.Zen.Test
{
    public class ApiClientHttpClientFactoryTests
    {
        [Test]
        public void Create_AddsCompressionHeaders()
        {
            using var httpClient = ApiClientHttpClientFactory.Create();

            var encodings = httpClient.DefaultRequestHeaders.AcceptEncoding.Select(header => header.Value).ToArray();

            Assert.That(encodings, Does.Contain("gzip"));
            Assert.That(encodings, Does.Contain("deflate"));
        }

        [Test]
        public void ReportingApiClient_Constructor_WithNullHttpClient_ThrowsArgumentNullException()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new ReportingAPIClient(null!));

            Assert.That(ex!.ParamName, Is.EqualTo("httpClient"));
        }

        [Test]
        public void RuntimeApiClient_Constructor_WithNullHttpClient_ThrowsArgumentNullException()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new RuntimeAPIClient(null!));

            Assert.That(ex!.ParamName, Is.EqualTo("httpClient"));
        }
    }
}
