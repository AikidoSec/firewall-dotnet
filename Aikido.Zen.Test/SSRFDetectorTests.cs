using System;
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
        public void HasSameHostAndPort_WhenComparingHostAndPort_ReturnsExpectedResult(string uri1, string uri2, bool expected)
        {
            var result = SSRFDetector.HasSameHostAndPort(new Uri(uri1), new Uri(uri2));

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void HasSameHostAndPort_WhenTrustProxyDisabled_ReturnsFalse()
        {
            Environment.SetEnvironmentVariable("AIKIDO_TRUST_PROXY", "false");

            var result = SSRFDetector.HasSameHostAndPort(
                new Uri("https://aikido.dev"),
                new Uri("https://aikido.dev/admin"));

            Assert.That(result, Is.False);
        }

        [TestCase("backend", true)]
        [TestCase("valid_hostname", true)]
        [TestCase("localhost", false)]
        [TestCase("metadata", false)]
        [TestCase("google.com", false)]
        [TestCase("127.0.0.1", false)]
        public void IsRequestToServiceHostname_MatchesNodeBehavior(string hostname, bool expected)
        {
            var result = SSRFDetector.IsRequestToServiceHostname(hostname);

            Assert.That(result, Is.EqualTo(expected));
        }

        [TestCase("localhost", "localhost")]
        [TestCase("LOCALHOST", "localhost")]
        [TestCase("ⓛocalhost", "localhost")]
        [TestCase("[::1]", "::1")]
        public void NormalizeHostname_ReturnsExpectedValue(string hostname, string expected)
        {
            var result = SSRFDetector.NormalizeHostname(hostname);

            Assert.That(result, Is.EqualTo(expected));
        }

        [TestCase("imds.test.com", "169.254.169.254", true)]
        [TestCase("metadata.google.internal", "169.254.169.254", false)]
        [TestCase("example.com", "127.0.0.1", false)]
        [TestCase("example.com", null, false)]
        public void IsStoredSSRF_MatchesNodeBehavior(string hostname, string? privateIPAddress, bool expected)
        {
            var result = SSRFDetector.IsStoredSSRF(hostname, privateIPAddress);

            Assert.That(result, Is.EqualTo(expected));
        }
    }
}
