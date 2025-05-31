using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Models.Events;
using Moq;

namespace Aikido.Zen.Test
{
    [TestFixture]
    public class SSRFHelperTests
    {
        private Context _context;
        private Mock<IZenApi> _mockZenApi;

        [SetUp]
        public void Setup()
        {
            _context = new Context();
            _mockZenApi = new Mock<IZenApi>();
            Agent.NewInstance(_mockZenApi.Object);
        }

        [Test]
        public void DetectSSRF_WithNullUri_ReturnsFalse()
        {
            // Act
            var result = SSRFHelper.DetectSSRF(null, _context, "test_module", "test_operation");

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void DetectSSRF_WithNullContext_ReturnsFalse()
        {
            // Arrange
            var uri = new Uri("http://example.com");

            // Act
            var result = SSRFHelper.DetectSSRF(uri, null, "test_module", "test_operation");

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void DetectSSRF_WithSafeUrl_ReturnsFalse()
        {
            // Arrange
            var uri = new Uri("https://example.com");
            _context.ParsedUserInput = new Dictionary<string, string> { { "url", "https://example.com" } };

            // Act
            var result = SSRFHelper.DetectSSRF(uri, _context, "test_module", "test_operation");

            // Assert
            Assert.That(result, Is.False);
            Assert.That(_context.AttackDetected, Is.False);
        }

        [Test]
        public void DetectSSRF_WithLocalhost_DetectsAttack()
        {
            // Arrange
            var uri = new Uri("http://localhost:8080");
            _context.ParsedUserInput = new Dictionary<string, string> { { "url", "http://localhost:8080" } };

            // Act
            var result = SSRFHelper.DetectSSRF(uri, _context, "test_module", "test_operation");

            // Assert
            Assert.That(result, Is.True);
            Assert.That(_context.AttackDetected, Is.True);
            _mockZenApi.Verify(a => a.Reporting.ReportAsync(
                It.IsAny<string>(),
                It.Is<DetectedAttack>(e =>
                    e.Attack.Kind == AttackKind.Ssrf.ToJsonName() &&
                    e.Attack.Source == Source.Body.ToJsonName() &&
                    e.Attack.Payload == "localhost" &&
                    e.Attack.Metadata["hostname"].ToString() == "localhost" &&
                    e.Attack.Metadata["port"].ToString() == "8080"
                ),
                It.IsAny<int>()
            ), Times.Once);
        }

        [Test]
        public void DetectSSRF_WithPrivateIP_DetectsAttack()
        {
            // Arrange
            var uri = new Uri("http://192.168.1.1:8080");
            _context.ParsedUserInput = new Dictionary<string, string> { { "url", "http://192.168.1.1:8080" } };

            // Act
            var result = SSRFHelper.DetectSSRF(uri, _context, "test_module", "test_operation");

            // Assert
            Assert.That(result, Is.True);
            Assert.That(_context.AttackDetected, Is.True);
            _mockZenApi.Verify(a => a.Reporting.ReportAsync(
                It.IsAny<string>(),
                It.Is<DetectedAttack>(e =>
                    e.Attack.Kind == AttackKind.Ssrf.ToJsonName() &&
                    e.Attack.Source == Source.Body.ToJsonName() &&
                    e.Attack.Payload == "192.168.1.1" &&
                    e.Attack.Metadata["hostname"].ToString() == "192.168.1.1" &&
                    e.Attack.Metadata["port"].ToString() == "8080"
                ),
                It.IsAny<int>()
            ), Times.Once);
        }

        [Test]
        public void DetectSSRF_WithRedirectToLocalhost_DetectsAttack()
        {
            // Arrange
            var uri = new Uri("https://localhost:8080");
            _context.OutgoingRequestRedirects.Add(new Context.RedirectInfo(
                uri,
                new Uri("http://localhost:8080")
            ));
            _context.ParsedUserInput = new Dictionary<string, string> { { "url", "https://example.com" } };

            // Act
            var result = SSRFHelper.DetectSSRF(uri, _context, "test_module", "test_operation");

            // Assert
            Assert.That(result, Is.True);
            Assert.That(_context.AttackDetected, Is.True);
            _mockZenApi.Verify(a => a.Reporting.ReportAsync(
                It.IsAny<string>(),
                It.Is<DetectedAttack>(e =>
                    e.Attack.Kind == AttackKind.Ssrf.ToJsonName() &&
                    e.Attack.Source == Source.Body.ToJsonName() &&
                    e.Attack.Payload == "localhost" &&
                    e.Attack.Metadata["hostname"].ToString() == "localhost" &&
                    e.Attack.Metadata["port"].ToString() == "8080" &&
                    e.Attack.Metadata["redirect_source"].ToString() == "https://example.com/" &&
                    e.Attack.Metadata["redirect_destination"].ToString() == "http://localhost:8080/"
                ),
                It.IsAny<int>()
            ), Times.Once);
        }

        [Test]
        public void DetectSSRF_WithExcessiveRedirects_DetectsAttack()
        {
            // Arrange
            var uri = new Uri("https://example.com");
            for (int i = 0; i < 11; i++)
            {
                _context.OutgoingRequestRedirects.Add(new Context.RedirectInfo(
                    new Uri($"https://redirect{i}.com"),
                    new Uri($"https://redirect{i + 1}.com")
                ));
            }
            _context.ParsedUserInput = new Dictionary<string, string> { { "url", "https://example.com" } };

            // Act
            var result = SSRFHelper.DetectSSRF(uri, _context, "test_module", "test_operation");

            // Assert
            Assert.That(result, Is.True);
            Assert.That(_context.AttackDetected, Is.True);
            _mockZenApi.Verify(a => a.Reporting.ReportAsync(
                It.IsAny<string>(),
                It.Is<DetectedAttack>(e =>
                    e.Attack.Kind == AttackKind.Ssrf.ToJsonName() &&
                    e.Attack.Source == Source.Body.ToJsonName() &&
                    e.Attack.Payload == "Excessive redirects detected" &&
                    e.Attack.Metadata["redirect_count"].ToString() == "11" &&
                    e.Attack.Metadata["max_redirects"].ToString() == "10"
                ),
                It.IsAny<int>()
            ), Times.Once);
        }

        [TestCase("127.0.0.1")]
        [TestCase("localhost")]
        [TestCase("127.0.0.1:8080")]
        [TestCase("localhost:8080")]
        public void DetectSSRF_WithLocalhostVariants_DetectsAttack(string host)
        {
            // Arrange
            var uri = new Uri($"http://{host}");
            _context.ParsedUserInput = new Dictionary<string, string> { { "url", $"http://{host}" } };

            // Act
            var result = SSRFHelper.DetectSSRF(uri, _context, "test_module", "test_operation");

            // Assert
            Assert.That(result, Is.True);
            Assert.That(_context.AttackDetected, Is.True);
            _mockZenApi.Verify(a => a.Reporting.ReportAsync(
                It.IsAny<string>(),
                It.Is<DetectedAttack>(e =>
                    e.Attack.Kind == AttackKind.Ssrf.ToJsonName() &&
                    e.Attack.Source == Source.Body.ToJsonName() &&
                    e.Attack.Payload == host
                ),
                It.IsAny<int>()
            ), Times.Once);
        }

        [Test]
        public void DetectSSRF_WithIPv6Localhost_DetectsAttack()
        {
            // Arrange
            var uri = new Uri("http://[::1]:8080");
            _context.ParsedUserInput = new Dictionary<string, string> { { "url", "http://[::1]:8080" } };

            // Act
            var result = SSRFHelper.DetectSSRF(uri, _context, "test_module", "test_operation");

            // Assert
            Assert.That(result, Is.True);
            Assert.That(_context.AttackDetected, Is.True);
            _mockZenApi.Verify(a => a.Reporting.ReportAsync(
                It.IsAny<string>(),
                It.Is<DetectedAttack>(e =>
                    e.Attack.Kind == AttackKind.Ssrf.ToJsonName() &&
                    e.Attack.Source == Source.Body.ToJsonName() &&
                    e.Attack.Payload == "::1" &&
                    e.Attack.Metadata["hostname"].ToString() == "::1" &&
                    e.Attack.Metadata["port"].ToString() == "8080"
                ),
                It.IsAny<int>()
            ), Times.Once);
        }
    }
}
