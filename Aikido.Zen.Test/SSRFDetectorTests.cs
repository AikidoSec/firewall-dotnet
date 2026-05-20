using Aikido.Zen.Core.Vulnerabilities;

namespace Aikido.Zen.Test
{
    [TestFixture]
    public class SSRFDetectorTests
    {
        private string? _originalTrustProxy;

        [SetUp]
        public void SetUp()
        {
            _originalTrustProxy = Environment.GetEnvironmentVariable("AIKIDO_TRUST_PROXY");
            Environment.SetEnvironmentVariable("AIKIDO_TRUST_PROXY", "true");
        }

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable("AIKIDO_TRUST_PROXY", _originalTrustProxy);
        }

        [Test]
        public void TryGetPrivateOrLocalIPAddress_WhenTargetIsDirectPrivateIp_ReturnsTrue()
        {
            var result = SSRFDetector.TryGetPrivateOrLocalIPAddress("127.0.0.1", out var privateIPAddress);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(privateIPAddress, Is.EqualTo("127.0.0.1"));
            });
        }

        [Test]
        public void TryGetPrivateOrLocalIPAddress_WhenTargetIsPublicIp_ReturnsFalse()
        {
            var result = SSRFDetector.TryGetPrivateOrLocalIPAddress("8.8.8.8", out var privateIPAddress);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(privateIPAddress, Is.Null);
            });
        }

        [TestCase("http://localhost", "http://localhost", true)]
        [TestCase("http://localhost", "http://localhost/path", true)]
        [TestCase("http://localhost", "http://localhost:8080", false)]
        [TestCase("http://aikido.dev:4000/private", "https://aikido.dev/admin", false)]
        [TestCase("ftp://localhost", "http://localhost", false)]
        [TestCase("https://aikido.dev", "https://google.com", false)]
        public void HasSameHostAndPort_WhenComparingHostAndPort_ReturnsExpectedResult(string left, string right, bool expected)
        {
            var rightUri = new Uri(right);
            var result = SSRFDetector.HasSameHostAndPort(new Uri(left), rightUri.Host, rightUri.Port);

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void HasSameHostAndPort_WhenTrustProxyDisabled_StillComparesHostAndPort()
        {
            Environment.SetEnvironmentVariable("AIKIDO_TRUST_PROXY", "false");

            var rightUri = new Uri("https://aikido.dev/admin");
            var result = SSRFDetector.HasSameHostAndPort(
                new Uri("https://aikido.dev"),
                rightUri.Host,
                rightUri.Port);

            Assert.That(result, Is.True);
        }

        [TestCase("http://localhost:8080/outbound", "http://localhost:8080", true)]
        [TestCase("http://localhost:80/outbound", "https://localhost/test/3", false)]
        [TestCase("http://localhost:443/outbound", "http://localhost/test/4", false)]
        [TestCase("http://localhost:4999/outbound", "http://localhost:5000/test/2", false)]
        [TestCase("http://app.local/outbound", "http://localhost:80", false)]
        public void HasSameHostAndPort_WhenComparingCurrentRequestToOutboundTarget_ReturnsExpectedResult(string serverUrl, string outboundUrl, bool expected)
        {
            var outboundUri = new Uri(outboundUrl);
            var result = SSRFDetector.HasSameHostAndPort(new Uri(serverUrl), outboundUri.Host, outboundUri.Port);

            Assert.That(result, Is.EqualTo(expected));
        }

        [TestCase("backend", true)]
        [TestCase("valid_hostname", true)]
        [TestCase("localhost", false)]
        [TestCase("metadata", false)]
        [TestCase("google.com", false)]
        [TestCase("127.0.0.1", false)]
        public void IsRequestToServiceHostname_ReturnsExpectedResult(string hostname, bool expected)
        {
            var result = SSRFDetector.IsRequestToServiceHostname(hostname);

            Assert.That(result, Is.EqualTo(expected));
        }

        [TestCase("localhost", "localhost")]
        [TestCase("LOCALHOST", "localhost")]
        [TestCase("\u24DBocalhost", "localhost")]
        [TestCase("[::1]", "::1")]
        public void NormalizeHostname_ReturnsExpectedValue(string hostname, string expected)
        {
            var result = SSRFDetector.NormalizeHostname(hostname);

            Assert.That(result, Is.EqualTo(expected));
        }

        [TestCase("imds.test.com", "169.254.169.254", true)]
        [TestCase("imds.test.com", "::ffff:169.254.169.254", true)]
        [TestCase("metadata.google.internal", "169.254.169.254", false)]
        [TestCase("169.254.169.254", "169.254.169.254", false)]
        [TestCase("example.com", "127.0.0.1", false)]
        [TestCase("example.com", null, false)]
        public void IsStoredSSRF_ReturnsExpectedResult(string hostname, string? privateIPAddress, bool expected)
        {
            var result = SSRFDetector.IsStoredSSRF(hostname, privateIPAddress);

            Assert.That(result, Is.EqualTo(expected));
        }
    }
}
