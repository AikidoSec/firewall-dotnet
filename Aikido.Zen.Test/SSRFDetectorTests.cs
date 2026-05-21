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

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void TryGetPrivateOrLocalIPAddress_WhenTargetIsBlank_ReturnsFalse(string? target)
        {
            var result = SSRFDetector.TryGetPrivateOrLocalIPAddress(target!, out var privateIPAddress);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(privateIPAddress, Is.Null);
            });
        }

        [Test]
        public void TryGetPrivateOrLocalIPAddress_WhenAddressListIsNull_ReturnsFalse()
        {
            var result = SSRFDetector.TryGetPrivateOrLocalIPAddress((IPAddress[])null!, out var privateIPAddress);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(privateIPAddress, Is.Null);
            });
        }

        [Test]
        public void TryGetPrivateOrLocalIPAddress_WhenAddressListContainsNull_ReturnsFalse()
        {
            var result = SSRFDetector.TryGetPrivateOrLocalIPAddress(new IPAddress[] { null! }, out var privateIPAddress);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(privateIPAddress, Is.Null);
            });
        }

        [Test]
        public void Detect_WhenTargetUriIsNull_AllowsAndSkipsStats()
        {
            var result = SSRFDetector.Detect(null!, null!, null!, out var inspectDns);

            Assert.Multiple(() =>
            {
                Assert.That(result.AttackKind, Is.Null);
                Assert.That(result.SkipStats, Is.True);
                Assert.That(inspectDns, Is.False);
            });
        }

        [Test]
        public void Detect_WhenCurrentRequestMatchesTarget_Allows()
        {
            var context = new Context { Url = "http://localhost:8080/api/request" };

            var result = SSRFDetector.Detect(
                new Uri("http://localhost:8080/admin"),
                new[] { IPAddress.Parse("127.0.0.1") },
                context,
                out var inspectDns);

            Assert.Multiple(() =>
            {
                Assert.That(result.AttackKind, Is.Null);
                Assert.That(inspectDns, Is.False);
            });
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
                new[] { IPAddress.Parse("127.0.0.1") },
                context,
                out var inspectDns);

            Assert.Multiple(() =>
            {
                Assert.That(result.AttackKind, Is.EqualTo(AttackKind.Ssrf));
                Assert.That(result.Source, Is.EqualTo(Source.Query));
                Assert.That(result.Payload, Is.EqualTo("http://localhost:8080/admin"));
                Assert.That(result.Paths, Is.EqualTo(new[] { ".url" }));
                Assert.That(inspectDns, Is.False);
            });
        }

        [Test]
        public void Detect_WhenResolvedAddressesArePublic_Allows()
        {
            var result = SSRFDetector.Detect(
                new Uri("http://public.example/path"),
                new[] { IPAddress.Parse("8.8.8.8") },
                new Context { Url = "https://app.local/outbound" },
                out var inspectDns);

            Assert.Multiple(() =>
            {
                Assert.That(result.AttackKind, Is.Null);
                Assert.That(inspectDns, Is.False);
            });
        }

        [Test]
        public void Detect_WhenHostnameNeedsDnsInspection_AllowsAndRequestsDnsInspection()
        {
            var result = SSRFDetector.Detect(
                new Uri("http://private.example/path"),
                null!,
                new Context { Url = "https://app.local/outbound" },
                out var inspectDns);

            Assert.Multiple(() =>
            {
                Assert.That(result.AttackKind, Is.Null);
                Assert.That(inspectDns, Is.True);
            });
        }

        [Test]
        public void Detect_WhenResolvedPrivateAddressDoesNotMatchUserInput_Allows()
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
                new[] { IPAddress.Parse("127.0.0.1") },
                context,
                out var inspectDns);

            Assert.Multiple(() =>
            {
                Assert.That(result.AttackKind, Is.Null);
                Assert.That(inspectDns, Is.False);
            });
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
                new[] { IPAddress.Parse("127.0.0.1") },
                context,
                out var inspectDns);

            Assert.Multiple(() =>
            {
                Assert.That(result.AttackKind, Is.Null);
                Assert.That(inspectDns, Is.False);
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
        public void HasSameHostAndPort_WhenPortIsNotProvided_MatchesHostOnly()
        {
            var result = SSRFDetector.HasSameHostAndPort(new Uri("https://aikido.dev"), "aikido.dev", null);

            Assert.That(result, Is.True);
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
        [TestCase("http://localhost:80/outbound", "https://localhost/test/3", false)]
        [TestCase("http://localhost:443/outbound", "http://localhost/test/4", false)]
        [TestCase("http://localhost:4999/outbound", "http://localhost:5000/test/2", false)]
        [TestCase("http://app.local/outbound", "http://localhost:80", false)]
        public void IsRequestToItself_WhenComparingCurrentRequestToOutboundTarget_ReturnsExpectedResult(string serverUrl, string outboundUrl, bool expected)
        {
            var outboundUri = new Uri(outboundUrl);
            var result = SSRFDetector.IsRequestToItself(new Uri(serverUrl), outboundUri.Host, outboundUri.Port);

            Assert.That(result, Is.EqualTo(expected));
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
