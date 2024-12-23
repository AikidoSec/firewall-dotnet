using Aikido.Zen.Core.Api;
using Moq;
using NUnit.Framework;
using System;
using System.Threading.Tasks;

namespace Aikido.Zen.Test
{
    public class RuntimeApiClientTests
    {
        private Mock<IRuntimeAPIClient> _runtimeApiClientMock;
        private IRuntimeAPIClient _runtimeApiClient;

        [SetUp]
        public void Setup()
        {
            _runtimeApiClientMock = new Mock<IRuntimeAPIClient>();
            _runtimeApiClient = _runtimeApiClientMock.Object;
        }

        [Test]
        public async Task GetConfigVersion_ShouldReturnSuccess()
        {
            // Arrange
            var expectedResponse = new ReportingAPIResponse { Success = true };
            _runtimeApiClientMock
                .Setup(r => r.GetConfigVersion(It.IsAny<string>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _runtimeApiClient.GetConfigVersion("token");

            // Assert
            Assert.AreEqual(expectedResponse, result);
        }

        [Test]
        public void GetConfigVersion_ShouldThrowExceptionOnError()
        {
            // Arrange
            _runtimeApiClientMock
                .Setup(r => r.GetConfigVersion(It.IsAny<string>()))
                .ThrowsAsync(new Exception("An error occurred while getting config version"));

            // Act & Assert
            Assert.ThrowsAsync<Exception>(async () => await _runtimeApiClient.GetConfigVersion("token"));
        }

        [Test]
        public async Task GetConfig_ShouldReturnSuccess()
        {
            // Arrange
            var expectedResponse = new ReportingAPIResponse { Success = true };
            _runtimeApiClientMock
                .Setup(r => r.GetConfig(It.IsAny<string>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _runtimeApiClient.GetConfig("token");

            // Assert
            Assert.AreEqual(expectedResponse, result);
        }

        [Test]
        public void GetConfig_ShouldThrowExceptionOnError()
        {
            // Arrange
            _runtimeApiClientMock
                .Setup(r => r.GetConfig(It.IsAny<string>()))
                .ThrowsAsync(new Exception("An error occurred while getting config"));

            // Act & Assert
            Assert.ThrowsAsync<Exception>(async () => await _runtimeApiClient.GetConfig("token"));
        }
    }
}
