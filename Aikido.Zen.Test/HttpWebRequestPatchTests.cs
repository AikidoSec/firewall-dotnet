using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Patches;
using Aikido.Zen.Test.Mocks;
using Moq;
using System.Net;

namespace Aikido.Zen.Test
{
    public class HttpWebRequestPatchTests
    {
        private Mock<WebRequest> _requestMock;
        private Uri _testUri;

        [SetUp]
        public void Setup()
        {
            _requestMock = new Mock<WebRequest>();
            _testUri = new Uri("http://example.com:8080/path");
            _requestMock.Setup(r => r.RequestUri).Returns(_testUri);
            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", "test-token");
            var apiMock = ZenApiMock.CreateMock();
            Agent.GetInstance(apiMock.Object);
        }

        [Test]
        public void CaptureRequest_ExtractsHostAndPort_ReturnsTrue()
        {
            // Act
            var result = HttpWebRequestPatch.CaptureRequest(_requestMock.Object);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void CaptureRequest_WithHttpsUri_ExtractsHostAndPort()
        {
            // Arrange
            var httpsUri = new Uri("https://secure.example.com:443/path");
            _requestMock.Setup(r => r.RequestUri).Returns(httpsUri);

            // Act
            var result = HttpWebRequestPatch.CaptureRequest(_requestMock.Object);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void CaptureRequest_WithDefaultPort_ExtractsHostAndPort()
        {
            // Arrange
            var defaultPortUri = new Uri("http://example.com/path");
            _requestMock.Setup(r => r.RequestUri).Returns(defaultPortUri);

            // Act
            var result = HttpWebRequestPatch.CaptureRequest(_requestMock.Object);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void CaptureRequest_WithNullUri_ThrowsException()
        {
            // Arrange
            _requestMock.Setup(r => r.RequestUri).Returns((Uri)null);

            // Act & Assert
            Assert.Throws<NullReferenceException>(() => 
                HttpWebRequestPatch.CaptureRequest(_requestMock.Object)
            );
        }
    }
}
