using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Sinks;

namespace Aikido.Zen.Test
{
    [TestFixture]
    public class InspectorTests
    {
        [Test]
        public void GetBlockedOperation_WhenSsrfHasSource_ReturnsSourcePath()
        {
            var result = InspectionResult.Block(
                AttackKind.Ssrf,
                source: Source.Query,
                paths: new[] { ".url" });

            var operation = Inspector.GetBlockedOperation("HttpClient.SendAsync", result);

            Assert.That(operation, Is.EqualTo("HttpClient.SendAsync originating from query.url"));
        }

        [Test]
        public void GetBlockedOperation_WhenSsrfHasSourceWithoutPath_ReturnsSource()
        {
            var result = InspectionResult.Block(
                AttackKind.Ssrf,
                source: Source.Body);

            var operation = Inspector.GetBlockedOperation("HttpClient.SendAsync", result);

            Assert.That(operation, Is.EqualTo("HttpClient.SendAsync originating from body"));
        }

        [Test]
        public void GetBlockedOperation_WhenStoredSsrf_ReturnsUnknownSource()
        {
            var result = InspectionResult.Block(AttackKind.StoredSsrf);

            var operation = Inspector.GetBlockedOperation("Dns.GetHostAddresses", result);

            Assert.That(operation, Is.EqualTo("Dns.GetHostAddresses originating from unknown source"));
        }

        [Test]
        public void GetBlockedOperation_WhenOutboundConnectionBlockedHasHostname_ReturnsHostname()
        {
            var result = InspectionResult.Block(
                AttackKind.OutboundConnectionBlocked,
                metadata: new Dictionary<string, string>
                {
                    { "hostname", "blocked.example" }
                });

            var operation = Inspector.GetBlockedOperation("HttpClient.SendAsync", result);

            Assert.That(operation, Is.EqualTo("HttpClient.SendAsync to blocked.example"));
        }

        [Test]
        public void GetBlockedOperation_WhenOutboundConnectionBlockedHasNoHostname_ReturnsOperation()
        {
            var result = InspectionResult.Block(AttackKind.OutboundConnectionBlocked);

            var operation = Inspector.GetBlockedOperation("HttpClient.SendAsync", result);

            Assert.That(operation, Is.EqualTo("HttpClient.SendAsync"));
        }

        [Test]
        public void GetBlockedOperation_WhenResultAllows_ReturnsOperation()
        {
            var operation = Inspector.GetBlockedOperation("HttpClient.SendAsync", InspectionResult.Allow());

            Assert.That(operation, Is.EqualTo("HttpClient.SendAsync"));
        }

        [Test]
        public void GetBlockedOperation_WhenResultIsNull_ReturnsOperation()
        {
            var operation = Inspector.GetBlockedOperation("HttpClient.SendAsync", null!);

            Assert.That(operation, Is.EqualTo("HttpClient.SendAsync"));
        }
    }
}
