using System.Net;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Models;
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
        public void TryGetPrivateOrLocalIPAddress_WhenAddressIsDirectPrivateIp_ReturnsTrue()
        {
            var result = SSRFDetector.TryGetPrivateOrLocalIPAddress(
                IPAddress.Parse("127.0.0.1"),
                out var privateIPAddress);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(privateIPAddress, Is.EqualTo("127.0.0.1"));
            });
        }

        [Test]
        public void TryGetPrivateOrLocalIPAddress_WhenAddressIsIPv4MappedIPv6_ReturnsMappedIPv4()
        {
            var result = SSRFDetector.TryGetPrivateOrLocalIPAddress(
                IPAddress.Parse("::ffff:127.0.0.1"),
                out var privateIPAddress);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(privateIPAddress, Is.EqualTo("127.0.0.1"));
            });
        }

        [Test]
        public void TryGetPrivateOrLocalIPAddress_WhenAddressIsPublicIp_ReturnsFalse()
        {
            var result = SSRFDetector.TryGetPrivateOrLocalIPAddress(
                IPAddress.Parse("8.8.8.8"),
                out var privateIPAddress);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(privateIPAddress, Is.Null);
            });
        }

        [Test]
        public void TryGetPrivateOrLocalIPAddress_WhenAddressIsNull_ReturnsFalse()
        {
            var result = SSRFDetector.TryGetPrivateOrLocalIPAddress((IPAddress)null!, out var privateIPAddress);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(privateIPAddress, Is.Null);
            });
        }

        [Test]
        public void Detect_WhenTargetUriIsNull_AllowsAndSkipsStats()
        {
            var result = SSRFDetector.Detect(null!, null!, null!);

            Assert.Multiple(() =>
            {
                Assert.That(result.AttackKind, Is.Null);
                Assert.That(result.SkipStats, Is.True);
            });
        }

        [Test]
        public void Detect_WhenContextIsNull_Allows()
        {
            var result = SSRFDetector.Detect(
                new Uri("http://private.example/path"),
                IPAddress.Parse("127.0.0.1"),
                null!);

            Assert.That(result.AttackKind, Is.Null);
        }

        [Test]
        public void Detect_WhenCurrentRequestMatchesTarget_Allows()
        {
            var context = new Context { Url = "http://localhost:8080/api/request" };

            var result = SSRFDetector.Detect(
                new Uri("http://localhost:8080/admin"),
                IPAddress.Parse("127.0.0.1"),
                context);

            Assert.That(result.AttackKind, Is.Null);
        }

        [Test]
        public void Detect_WhenTrustProxyDisabledAndPrivateTargetMatchesUserInput_BlocksSsrf()
        {
            Environment.SetEnvironmentVariable("AIKIDO_TRUST_PROXY", "false");
            var context = new Context
            {
                Url = "http://localhost:8080/api/request",
                ParsedUserInput = new Dictionary<string, string>
                {
                    { "query.url", "http://localhost:8080/admin" }
                }
            };

            var result = SSRFDetector.Detect(
                new Uri("http://localhost:8080/admin"),
                IPAddress.Parse("127.0.0.1"),
                context);

            Assert.Multiple(() =>
            {
                Assert.That(result.AttackKind, Is.EqualTo(AttackKind.Ssrf));
                Assert.That(result.Source, Is.EqualTo(Source.Query));
                Assert.That(result.Payload, Is.EqualTo("http://localhost:8080/admin"));
                Assert.That(result.Paths, Is.EqualTo(new[] { ".url" }));
            });
        }

        [Test]
        public void Detect_WhenPrivateTargetMatchesSchemalessUserInput_BlocksSsrf()
        {
            var context = new Context
            {
                Url = "http://app.local/api/request",
                ParsedUserInput = new Dictionary<string, string>
                {
                    { "query.url", "localhost:8080/admin" }
                }
            };

            var result = SSRFDetector.Detect(
                new Uri("http://localhost:8080/admin"),
                IPAddress.Parse("127.0.0.1"),
                context);

            Assert.Multiple(() =>
            {
                Assert.That(result.AttackKind, Is.EqualTo(AttackKind.Ssrf));
                Assert.That(result.Source, Is.EqualTo(Source.Query));
                Assert.That(result.Payload, Is.EqualTo("localhost:8080/admin"));
                Assert.That(result.Paths, Is.EqualTo(new[] { ".url" }));
            });
        }

        [Test]
        public void Detect_WhenRemoteAddressIsPublic_Allows()
        {
            var result = SSRFDetector.Detect(
                new Uri("http://public.example/path"),
                IPAddress.Parse("8.8.8.8"),
                new Context { Url = "https://app.local/outbound" });

            Assert.That(result.AttackKind, Is.Null);
        }

        [Test]
        public void Detect_WhenPrivateImdsAddressHasNoUserInput_BlocksStoredSsrf()
        {
            var result = SSRFDetector.Detect(
                new Uri("http://imds.test.com/latest/meta-data"),
                IPAddress.Parse("169.254.169.254"),
                new Context { Url = "https://app.local/outbound" });

            Assert.Multiple(() =>
            {
                Assert.That(result.AttackKind, Is.EqualTo(AttackKind.StoredSsrf));
                Assert.That(result.Metadata["hostname"], Is.EqualTo("imds.test.com"));
                Assert.That(result.Metadata["privateIP"], Is.EqualTo("169.254.169.254"));
            });
        }

        [Test]
        public void Detect_WhenRemoteAddressIsMissing_Allows()
        {
            var result = SSRFDetector.Detect(
                new Uri("http://private.example/path"),
                null!,
                new Context { Url = "https://app.local/outbound" });

            Assert.That(result.AttackKind, Is.Null);
        }

        [Test]
        public void Detect_WhenPrivateRemoteAddressDoesNotMatchUserInput_Allows()
        {
            var context = new Context
            {
                Url = "https://app.local/outbound",
                ParsedUserInput = new Dictionary<string, string>
                {
                    { "query.url", "not a url" },
                    { "body.url", "http://other.example/admin" }
                }
            };

            var result = SSRFDetector.Detect(
                new Uri("http://private.example/admin"),
                IPAddress.Parse("127.0.0.1"),
                context);

            Assert.That(result.AttackKind, Is.Null);
        }

        [Test]
        public void Detect_WhenPrivateServiceHostnameMatchesUserInput_Allows()
        {
            var context = new Context
            {
                Url = "https://app.local/outbound",
                ParsedUserInput = new Dictionary<string, string>
                {
                    { "query.url", "http://backend/admin" }
                }
            };

            var result = SSRFDetector.Detect(
                new Uri("http://backend/admin"),
                IPAddress.Parse("127.0.0.1"),
                context);

            Assert.That(result.AttackKind, Is.Null);
        }

        [TestCase("http://localhost", "localhost", 80, true)]
        [TestCase("localhost", "localhost", 80, true)]
        [TestCase("localhost/path/path", "localhost", 80, true)]
        [TestCase("localhost:8080/admin", "localhost", 8080, true)]
        [TestCase("localhost:8080/admin", "localhost", 4321, false)]
        [TestCase("ftp://localhost", "localhost", 80, false)]
        [TestCase("https://aikido.dev/admin", "google.com", 443, false)]
        [TestCase("http://", "localhost", 80, false)]
        [TestCase("", "localhost", 80, false)]
        public void FindHostnameInUserInput_ReturnsExpectedResult(string userInput, string hostname, int port, bool expected)
        {
            var result = SSRFDetector.FindHostnameInUserInput(userInput, hostname, port);

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void FindHostnameInUserInput_WhenPortIsNotProvided_MatchesHostOnly()
        {
            var result = SSRFDetector.FindHostnameInUserInput("https://aikido.dev/admin", "aikido.dev", null);

            Assert.That(result, Is.True);
        }

        [Test]
        public void IsRequestToItself_WhenTrustProxyDisabled_ReturnsFalse()
        {
            Environment.SetEnvironmentVariable("AIKIDO_TRUST_PROXY", "false");

            var rightUri = new Uri("https://aikido.dev/admin");
            var result = SSRFDetector.IsRequestToItself(
                new Uri("https://aikido.dev"),
                rightUri.Host,
                rightUri.Port);

            Assert.That(result, Is.False);
        }

        [TestCase("http://localhost:8080/outbound", "http://localhost:8080", true)]
        [TestCase("http://localhost:80/outbound", "https://localhost/test/3", true)]
        [TestCase("http://localhost:443/outbound", "http://localhost/test/4", true)]
        [TestCase("http://localhost:4999/outbound", "http://localhost:5000/test/2", false)]
        [TestCase("http://app.local/outbound", "http://localhost:80", false)]
        public void IsRequestToItself_WhenComparingCurrentRequestToOutboundTarget_ReturnsExpectedResult(string serverUrl, string outboundUrl, bool expected)
        {
            var outboundUri = new Uri(outboundUrl);
            var result = SSRFDetector.IsRequestToItself(new Uri(serverUrl), outboundUri.Host, outboundUri.Port);

            Assert.That(result, Is.EqualTo(expected));
        }

        [TestCase("")]
        [TestCase(" ")]
        public void IsRequestToItself_WhenOutboundHostnameIsBlank_ReturnsFalse(string outboundHostname)
        {
            var result = SSRFDetector.IsRequestToItself(new Uri("http://localhost/outbound"), outboundHostname, 80);

            Assert.That(result, Is.False);
        }

        [TestCase("backend", true)]
        [TestCase("valid_hostname", true)]
        [TestCase("localhost", false)]
        [TestCase("metadata", false)]
        [TestCase("google.com", false)]
        [TestCase("127.0.0.1", false)]
        [TestCase(null, false)]
        [TestCase(" ", false)]
        public void IsRequestToServiceHostname_ReturnsExpectedResult(string? hostname, bool expected)
        {
            var result = SSRFDetector.IsRequestToServiceHostname(hostname!);

            Assert.That(result, Is.EqualTo(expected));
        }

        [TestCase("localhost", "localhost")]
        [TestCase("LOCALHOST", "localhost")]
        [TestCase("\u24DBocalhost", "localhost")]
        [TestCase("[::1]", "::1")]
        [TestCase(null, null)]
        [TestCase(" ", " ")]
        [TestCase("\uD800", "\uD800")]
        public void NormalizeHostname_ReturnsExpectedValue(string? hostname, string? expected)
        {
            var result = SSRFDetector.NormalizeHostname(hostname!);

            Assert.That(result, Is.EqualTo(expected));
        }

        [TestCase("imds.test.com", "169.254.169.254", true)]
        [TestCase("imds.test.com", "fd00:ec2::254", true)]
        [TestCase("imds.test.com", "100.100.100.200", true)]
        [TestCase("imds.test.com", "::ffff:169.254.169.254", true)]
        [TestCase("imds.test.com", "::ffff:100.100.100.200", true)]
        [TestCase("imds.test.com", "0::ffff:6464:64c8", true)]
        [TestCase("imds.test.com", "0000:0000:0:0000:0000:ffff:a9fe:a9fe", true)]
        [TestCase("imds.test.com", "fd00:ec2:0:0000:0:0:0000:0254", true)]
        [TestCase("imds.test.com", " ", false)]
        [TestCase("metadata.google.internal", "169.254.169.254", false)]
        [TestCase("metadata.goog", "169.254.169.254", false)]
        [TestCase("METADATA.GOOGLE.INTERNAL", "169.254.169.254", false)]
        [TestCase(null, "169.254.169.254", false)]
        [TestCase(" ", "169.254.169.254", false)]
        [TestCase("169.254.169.254", "169.254.169.254", false)]
        [TestCase("example.com", "127.0.0.1", false)]
        [TestCase("example.com", "1.2.3.4", false)]
        [TestCase("example.com", "169.254.169.253", false)]
        [TestCase("example.com", "example.com", false)]
        [TestCase("example.com", null, false)]
        public void IsStoredSSRF_ReturnsExpectedResult(string? hostname, string? privateIPAddress, bool expected)
        {
            var result = SSRFDetector.IsStoredSSRF(hostname!, privateIPAddress);

            Assert.That(result, Is.EqualTo(expected));
        }
    }
}
