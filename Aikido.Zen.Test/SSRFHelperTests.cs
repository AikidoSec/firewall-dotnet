using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Models.Events;
using Aikido.Zen.Tests.Mocks;
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
            _mockZenApi = ZenApiMock.CreateMock();
            Agent.NewInstance(_mockZenApi.Object);
            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", "<token>");
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
            _context.AbsoluteUrl = "http://localhost:1234";
            _context.ParsedUserInput = new Dictionary<string, string> { { "query.url", "http://localhost:8080" } };

            // Act
            var result = SSRFHelper.DetectSSRF(uri, _context, "test_module", "test_operation");

            // Assert
            Assert.That(result, Is.True);
            Assert.That(_context.AttackDetected, Is.True);
        }

        [Test]
        public void DetectSSRF_WithPrivateIP_DetectsAttack()
        {
            // Arrange
            var uri = new Uri("http://192.168.1.1:8080");
            _context.AbsoluteUrl = "http://localhost:1234";
            _context.ParsedUserInput = new Dictionary<string, string> { { "query.url", "http://192.168.1.1:8080" } };

            // Act
            var result = SSRFHelper.DetectSSRF(uri, _context, "test_module", "test_operation");

            // Assert
            Assert.That(result, Is.True);
            Assert.That(_context.AttackDetected, Is.True);
        }

        [Test]
        public void DetectSSRF_WithRedirectToLocalhost_DetectsAttack()
        {
            // Arrange
            var src = new Uri("https://example.com");
            var dest = new Uri("http://localhost:8080");
            _context.AbsoluteUrl = "https://localhost:1234";
            _context.Url = "/";
            _context.OutgoingRequestRedirects.Add(new Context.RedirectInfo(
                src: src,
                dest: dest
            ));
            _context.ParsedUserInput = new Dictionary<string, string> { { "query.url", src.ToString() } };

            // Act
            var result = SSRFHelper.DetectSSRF(dest, _context, "test_module", "test_operation");
            Task.Delay(100).Wait(); // Simulate async delay for reporting

            // Assert
            Assert.That(result, Is.True);
            Assert.That(_context.AttackDetected, Is.True);
        }

        [TestCase("127.0.0.1")]
        [TestCase("localhost")]
        [TestCase("127.0.0.1:8080")]
        [TestCase("localhost:8080")]
        public void DetectSSRF_WithLocalhostVariants_DetectsAttack(string host)
        {
            // Arrange
            var uri = new Uri($"http://{host}");
            _context.ParsedUserInput = new Dictionary<string, string> { { "query.url", $"http://{host}" } };

            // Act
            var result = SSRFHelper.DetectSSRF(uri, _context, "test_module", "test_operation");

            // Assert
            Assert.That(result, Is.True);
            Assert.That(_context.AttackDetected, Is.True);
        }

        [Test]
        public void DetectSSRF_WithIPv6Localhost_DetectsAttack()
        {
            // Arrange
            var uri = new Uri("http://[::1]:8080");
            _context.AbsoluteUrl = "http://[::1]:1234";
            _context.ParsedUserInput = new Dictionary<string, string> { { "query.url", "http://[::1]:8080" } };

            // Act
            var result = SSRFHelper.DetectSSRF(uri, _context, "test_module", "test_operation");

            // Assert
            Assert.That(result, Is.True);
            Assert.That(_context.AttackDetected, Is.True);
        }
    }
}
