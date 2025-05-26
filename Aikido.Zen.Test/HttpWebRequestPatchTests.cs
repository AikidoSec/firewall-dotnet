using System.Net;
using System.Reflection;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Patches;
using Aikido.Zen.Tests.Mocks;
using Moq;

namespace Aikido.Zen.Test
{
    public class HttpWebRequestPatchTests
    {
        private Mock<WebRequest> _requestMock;
        private Uri _testUri;
        private Context _context;
        private MethodInfo _methodInfo;

        [SetUp]
        public void Setup()
        {
            _requestMock = new Mock<WebRequest>();
            _testUri = new Uri("http://example.com:8080/path");
            _requestMock.Setup(r => r.RequestUri).Returns(_testUri);
            _context = new Context();
            _methodInfo = typeof(WebRequest).GetMethod("GetResponse");

            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", "test-token");
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "true");
            var apiMock = ZenApiMock.CreateMock();
            Agent.NewInstance(apiMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", null);
            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", null);
        }

        [Test]
        public void CaptureRequest_WithSafeUrl_ReturnsTrue()
        {
            // Arrange
            var safeUri = new Uri("https://example.com/path");
            _requestMock.Setup(r => r.RequestUri).Returns(safeUri);
            _context.ParsedUserInput = new Dictionary<string, string> { { "url", safeUri.ToString() } };

            // Act
            var result = WebRequestPatcher.OnWebRequest(_requestMock.Object, _methodInfo, _context);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(_context.AttackDetected, Is.False);
        }

        [Test]
        public void CaptureRequest_WithLocalhostUrl_ThrowsException()
        {
            // Arrange
            var localhostUri = new Uri("http://localhost:8080/path");
            _requestMock.Setup(r => r.RequestUri).Returns(localhostUri);
            _context.ParsedUserInput = new Dictionary<string, string> { { "url", localhostUri.ToString() } };

            // Act & Assert
            Assert.Throws<AikidoException>(() =>
                WebRequestPatcher.OnWebRequest(_requestMock.Object, _methodInfo, _context)
            );
            Assert.That(_context.AttackDetected, Is.True);
        }

        [Test]
        public void CaptureRequest_WithPrivateIP_ThrowsException()
        {
            // Arrange
            var privateIpUri = new Uri("http://192.168.1.1:8080/path");
            _requestMock.Setup(r => r.RequestUri).Returns(privateIpUri);
            _context.ParsedUserInput = new Dictionary<string, string> { { "url", privateIpUri.ToString() } };

            // Act & Assert
            Assert.Throws<AikidoException>(() =>
                WebRequestPatcher.OnWebRequest(_requestMock.Object, _methodInfo, _context)
            );
            Assert.That(_context.AttackDetected, Is.True);
        }

        [Test]
        public void CaptureRequest_WithBlockingDisabled_ReturnsTrue()
        {
            // Arrange
            Environment.SetEnvironmentVariable("AIKIDO_BLOCK", "false");
            var localhostUri = new Uri("http://localhost:8080/path");
            _requestMock.Setup(r => r.RequestUri).Returns(localhostUri);
            _context.ParsedUserInput = new Dictionary<string, string> { { "url", localhostUri.ToString() } };

            // Act
            var result = WebRequestPatcher.OnWebRequest(_requestMock.Object, _methodInfo, _context);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(_context.AttackDetected, Is.True);
        }

        [Test]
        public void CaptureRequest_WithRedirectToLocalhost_ThrowsException()
        {
            // Arrange
            var safeUri = new Uri("https://example.com/path");
            _requestMock.Setup(r => r.RequestUri).Returns(safeUri);
            _context.ParsedUserInput = new Dictionary<string, string> { { "url", safeUri.ToString() } };
            _context.OutgoingRequestRedirects.Add(new Context.RedirectInfo(
                safeUri,
                new Uri("http://localhost:8080/path")
            ));

            // Act & Assert
            Assert.Throws<AikidoException>(() =>
                WebRequestPatcher.OnWebRequest(_requestMock.Object, _methodInfo, _context)
            );
            Assert.That(_context.AttackDetected, Is.True);
        }

        [Test]
        public void CaptureRequest_WithMultipleRedirects_ThrowsException()
        {
            // Arrange
            var safeUri = new Uri("https://example.com/path");
            _requestMock.Setup(r => r.RequestUri).Returns(safeUri);
            _context.ParsedUserInput = new Dictionary<string, string> { { "url", safeUri.ToString() } };
            _context.OutgoingRequestRedirects.Add(new Context.RedirectInfo(
                safeUri,
                new Uri("https://redirect1.com/path")
            ));
            _context.OutgoingRequestRedirects.Add(new Context.RedirectInfo(
                new Uri("https://redirect1.com/path"),
                new Uri("http://localhost:8080/path")
            ));

            // Act & Assert
            Assert.Throws<AikidoException>(() =>
                WebRequestPatcher.OnWebRequest(_requestMock.Object, _methodInfo, _context)
            );
            Assert.That(_context.AttackDetected, Is.True);
        }

        [Test]
        public void CaptureRequest_WithNullUri_ThrowsException()
        {
            // Arrange
            _requestMock.Setup(r => r.RequestUri).Returns((Uri)null);

            // Act & Assert
            Assert.Throws<NullReferenceException>(() =>
                WebRequestPatcher.OnWebRequest(_requestMock.Object, _methodInfo, _context)
            );
        }
    }
}
