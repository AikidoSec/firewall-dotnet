using System;
using System.Collections.Generic;
using System.Linq;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Vulnerabilities;

namespace Aikido.Zen.Test
{
    [TestFixture]
    public class SsrfDetectorTests
    {
        [SetUp]
        public void Setup()
        {
            Environment.SetEnvironmentVariable("AIKIDO_TRUST_PROXY", "true");
            SsrfDetector.ResetDnsResolver();
        }

        [TearDown]
        public void TearDown()
        {
            SsrfDetector.ResetDnsResolver();
        }

        [Test]
        public void Detect_DirectSsrf_WhenHostnameAppearsInUserInput_ReturnsSsrf()
        {
            var context = new Context
            {
                Url = "https://app.example.com/profile",
                ParsedUserInput = new Dictionary<string, string>
                {
                    ["query.url"] = "http://127.0.0.1/admin"
                }
            };

            var result = SsrfDetector.Detect(new Uri("http://127.0.0.1/admin"), context);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Kind, Is.EqualTo(AttackKind.Ssrf));
            Assert.That(result.Source, Is.EqualTo(Source.Query));
            Assert.That(result.Payload, Is.EqualTo("http://127.0.0.1/admin"));
            Assert.That(result.Metadata["hostname"], Is.EqualTo("127.0.0.1"));
            Assert.That(result.Metadata["port"], Is.EqualTo(80));
        }

        [Test]
        public void Detect_DnsResolvedImdsIp_WithoutContextMatch_ReturnsStoredSsrf()
        {
            SsrfDetector.SetDnsResolver(new FakeDnsResolver(("metadata.attacker.example", "169.254.169.254")));

            var result = SsrfDetector.Detect(new Uri("https://metadata.attacker.example/"), null);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Kind, Is.EqualTo(AttackKind.StoredSsrf));
            Assert.That(result.Source, Is.Null);
            Assert.That(result.Payload, Is.Null);
            Assert.That(result.Metadata["hostname"], Is.EqualTo("metadata.attacker.example"));
            Assert.That(result.Metadata["privateIP"], Is.EqualTo("169.254.169.254"));
        }

        [Test]
        public void Detect_WhenTargetIsServiceHostname_ReturnsNull()
        {
            SsrfDetector.SetDnsResolver(new FakeDnsResolver(("backend", "10.0.0.10")));
            var context = new Context
            {
                ParsedUserInput = new Dictionary<string, string>
                {
                    ["query.url"] = "http://backend/internal"
                }
            };

            var result = SsrfDetector.Detect(new Uri("http://backend/internal"), context);

            Assert.That(result, Is.Null);
        }

        private sealed class FakeDnsResolver : IDnsResolver
        {
            private readonly Dictionary<string, string[]> _entries;

            public FakeDnsResolver(params (string Hostname, string Address)[] entries)
            {
                _entries = entries
                    .GroupBy(entry => entry.Hostname, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.Select(entry => entry.Address).ToArray(), StringComparer.OrdinalIgnoreCase);
            }

            public string[] ResolveHostAddresses(string hostname)
            {
                return _entries.TryGetValue(hostname, out var addresses) ? addresses : Array.Empty<string>();
            }
        }
    }
}
