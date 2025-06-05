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
            var result = WebRequestPatcher.OnWebRequestStarted(_requestMock.Object, _methodInfo, _context);

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
                WebRequestPatcher.OnWebRequestStarted(_requestMock.Object, _methodInfo, _context)
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
                WebRequestPatcher.OnWebRequestStarted(_requestMock.Object, _methodInfo, _context)
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
            var result = WebRequestPatcher.OnWebRequestStarted(_requestMock.Object, _methodInfo, _context);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(_context.AttackDetected, Is.True);
        }

        [Test]
        public void CaptureRequest_WithNullUri_ReturnsTrue()
        {
            // Arrange
            _requestMock.Setup(r => r.RequestUri).Returns((Uri)null);

            // Act
            var result = WebRequestPatcher.OnWebRequestStarted(_requestMock.Object, _methodInfo, _context);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void OnWebRequestStarted_CallsCaptureOutboundRequest_WithCorrectParameters()
        {
            // Arrange
            Agent.Instance.ClearContext(); // Ensure clean context for this test
            var requestUri = new Uri("http://capture.example.com:1234/test");
            _requestMock.Setup(r => r.RequestUri).Returns(requestUri);

            // Act
            WebRequestPatcher.OnWebRequestStarted(_requestMock.Object, _methodInfo, _context);

            // Assert
            Assert.That(Agent.Instance.Context.Hostnames.Count(), Is.EqualTo(1));
            var capturedHost = Agent.Instance.Context.Hostnames.FirstOrDefault();
            Assert.Multiple(() =>
            {
                Assert.That(capturedHost.Hostname, Is.EqualTo("capture.example.com"));
                Assert.That(capturedHost.Port, Is.EqualTo(1234));
            });
        }

        // Tests for OnWebRequestFinished
        [Test]
        public void OnWebRequestFinished_WithNullRequest_ReturnsVoid()
        {
            // Arrange
            var responseMock = new Mock<WebResponse>();

            // Act & Assert
            Assert.DoesNotThrow(() => WebRequestPatcher.OnWebRequestFinished(null, responseMock.Object, _context));
            Assert.That(_context.OutgoingRequestRedirects, Is.Empty);
        }

        [Test]
        public void OnWebRequestFinished_WithNullResponse_ReturnsVoid()
        {
            // Arrange
            // Act & Assert
            Assert.DoesNotThrow(() => WebRequestPatcher.OnWebRequestFinished(_requestMock.Object, null, _context));
            Assert.That(_context.OutgoingRequestRedirects, Is.Empty);
        }

        [Test]
        public void OnWebRequestFinished_WithNullContext_ReturnsVoid()
        {
            // Arrange
            var responseMock = new Mock<WebResponse>();

            // Act & Assert
            Assert.DoesNotThrow(() => WebRequestPatcher.OnWebRequestFinished(_requestMock.Object, responseMock.Object, null));
        }

        [Test]
        public void OnWebRequestFinished_WithNonHttpWebResponse_DoesNotAddRedirect()
        {
            // Arrange
            var responseMock = new Mock<WebResponse>(); // Not an HttpWebResponse
            _requestMock.Setup(r => r.RequestUri).Returns(new Uri("http://source.com"));

            // Act
            WebRequestPatcher.OnWebRequestFinished(_requestMock.Object, responseMock.Object, _context);

            // Assert
            Assert.That(_context.OutgoingRequestRedirects, Is.Empty);
        }

        [TestCase(HttpStatusCode.Redirect)]
        [TestCase(HttpStatusCode.MovedPermanently)] // 301
        [TestCase(HttpStatusCode.TemporaryRedirect)] // 307
        [TestCase((HttpStatusCode)308)] // PermanentRedirect
        public void OnWebRequestFinished_WithHttpRedirectAndLocationHeader_AddsRedirect(HttpStatusCode statusCode)
        {
            // Arrange
            var sourceUri = new Uri("http://source.com");
            var destinationUri = new Uri("http://destination.com");
            _requestMock.Setup(r => r.RequestUri).Returns(sourceUri);

            var httpResponseMock = new Mock<HttpWebResponse>();
            httpResponseMock.Setup(r => r.StatusCode).Returns(statusCode);
            httpResponseMock.Setup(r => r.Headers).Returns(new WebHeaderCollection { { "Location", destinationUri.ToString() } });

            // Act
            WebRequestPatcher.OnWebRequestFinished(_requestMock.Object, httpResponseMock.Object, _context);

            // Assert
            Assert.That(_context.OutgoingRequestRedirects, Has.Count.EqualTo(1));
            var redirectInfo = _context.OutgoingRequestRedirects.First(); Assert.Multiple(() =>
            {
                Assert.That(redirectInfo.Source, Is.EqualTo(sourceUri));
                Assert.That(redirectInfo.Destination, Is.EqualTo(destinationUri));
            });
        }

        [Test]
        public void OnWebRequestFinished_WithHttpRedirectAndNoLocationHeader_DoesNotAddRedirect()
        {
            // Arrange
            var sourceUri = new Uri("http://source.com");
            _requestMock.Setup(r => r.RequestUri).Returns(sourceUri);

            var httpResponseMock = new Mock<HttpWebResponse>();
            httpResponseMock.Setup(r => r.StatusCode).Returns(HttpStatusCode.Redirect);
            httpResponseMock.Setup(r => r.Headers).Returns(new WebHeaderCollection()); // No Location header

            // Act
            WebRequestPatcher.OnWebRequestFinished(_requestMock.Object, httpResponseMock.Object, _context);

            // Assert
            Assert.That(_context.OutgoingRequestRedirects, Is.Empty);
        }

        [Test]
        public void OnWebRequestFinished_WithHttpRedirectAndInvalidLocationHeader_DoesNotAddRedirect()
        {
            // Arrange
            var sourceUri = new Uri("http://source.com");
            _requestMock.Setup(r => r.RequestUri).Returns(sourceUri);

            var httpResponseMock = new Mock<HttpWebResponse>();
            httpResponseMock.Setup(r => r.StatusCode).Returns(HttpStatusCode.Redirect);
            httpResponseMock.Setup(r => r.Headers).Returns(new WebHeaderCollection { { "Location", "not_a_valid_uri" } });

            // Act
            WebRequestPatcher.OnWebRequestFinished(_requestMock.Object, httpResponseMock.Object, _context);

            // Assert
            Assert.That(_context.OutgoingRequestRedirects, Is.Empty);
        }

        [Test]
        public void OnWebRequestFinished_WithHttpNonRedirectStatus_DoesNotAddRedirect()
        {
            // Arrange
            var sourceUri = new Uri("http://source.com");
            _requestMock.Setup(r => r.RequestUri).Returns(sourceUri);

            var httpResponseMock = new Mock<HttpWebResponse>();
            httpResponseMock.Setup(r => r.StatusCode).Returns(HttpStatusCode.OK);
            httpResponseMock.Setup(r => r.Headers).Returns(new WebHeaderCollection { { "Location", "http://destination.com" } });

            // Act
            WebRequestPatcher.OnWebRequestFinished(_requestMock.Object, httpResponseMock.Object, _context);

            // Assert
            Assert.That(_context.OutgoingRequestRedirects, Is.Empty);
        }
    }
}
