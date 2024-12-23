using Aikido.Zen.Core.Api;
using Moq;
using NUnit.Framework;
using System;
using System.Threading.Tasks;

namespace Aikido.Zen.Test
{
    public class ReportingAPiClientTests
    {
        private Mock<IReportingAPIClient> _reportingApiClientMock;
        private IReportingAPIClient _reportingApiClient;

        [SetUp]
        public void Setup()
        {
            _reportingApiClientMock = new Mock<IReportingAPIClient>();
            _reportingApiClient = _reportingApiClientMock.Object;
        }

        [Test]
        public async Task ReportAsync_ShouldReturnSuccess()
        {
            // Arrange
            var expectedResponse = new ReportingAPIResponse { Success = true };
            _reportingApiClientMock
                .Setup(r => r.ReportAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<int>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _reportingApiClient.ReportAsync("token", new { }, 5000);

            // Assert
            Assert.AreEqual(expectedResponse, result);
        }

        [Test]
        public void ReportAsync_ShouldThrowExceptionOnError()
        {
            // Arrange
            _reportingApiClientMock
                .Setup(r => r.ReportAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<int>()))
                .ThrowsAsync(new Exception("An error occurred while reporting"));

            // Act & Assert
            Assert.ThrowsAsync<Exception>(async () => await _reportingApiClient.ReportAsync("token", new { }, 5000));
        }

        [Test]
        public async Task GetBlockedIps_ShouldReturnSuccess()
        {
            // Arrange
            var expectedResponse = new BlockedIpsAPIResponse { Success = true };
            _reportingApiClientMock
                .Setup(r => r.GetBlockedIps(It.IsAny<string>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _reportingApiClient.GetBlockedIps("token");

            // Assert
            Assert.AreEqual(expectedResponse, result);
        }

        [Test]
        public void GetBlockedIps_ShouldThrowExceptionOnError()
        {
            // Arrange
            _reportingApiClientMock
                .Setup(r => r.GetBlockedIps(It.IsAny<string>()))
                .ThrowsAsync(new Exception("An error occurred while getting blocked IPs"));

            // Act & Assert
            Assert.ThrowsAsync<Exception>(async () => await _reportingApiClient.GetBlockedIps("token"));
        }
    }
}
