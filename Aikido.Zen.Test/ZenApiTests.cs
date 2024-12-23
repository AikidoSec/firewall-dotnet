using Aikido.Zen.Core.Api;
using Moq;
using NUnit.Framework;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Aikido.Zen.Test
{
    public class ZenApiTests
    {
        private Mock<IReportingAPIClient> _reportingApiClientMock;
        private Mock<IRuntimeAPIClient> _runtimeApiClientMock;
        private IZenApi _zenApi;

        [SetUp]
        public void Setup()
        {
            _reportingApiClientMock = new Mock<IReportingAPIClient>();
            _runtimeApiClientMock = new Mock<IRuntimeAPIClient>();

            var zenApiMock = new Mock<IZenApi>();
            zenApiMock.Setup(z => z.Reporting).Returns(_reportingApiClientMock.Object);
            zenApiMock.Setup(z => z.Runtime).Returns(_runtimeApiClientMock.Object);

            _zenApi = zenApiMock.Object;
        }

        [Test]
        public async Task ReportingApiClient_ReportAsync_ShouldReturnSuccess()
        {
            // Arrange
            var expectedResponse = new ReportingAPIResponse { Success = true };
            _reportingApiClientMock
                .Setup(r => r.ReportAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<int>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _zenApi.Reporting.ReportAsync("token", new { }, 5000);

            // Assert
            Assert.AreEqual(expectedResponse, result);
        }

        [Test]
        public void ReportingApiClient_ReportAsync_ShouldThrowExceptionOnError()
        {
            // Arrange
            _reportingApiClientMock
                .Setup(r => r.ReportAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<int>()))
                .ThrowsAsync(new Exception("An error occurred while reporting"));

            // Act & Assert
            Assert.ThrowsAsync<Exception>(async () => await _zenApi.Reporting.ReportAsync("token", new { }, 5000));
        }

        [Test]
        public async Task RuntimeApiClient_GetConfigVersion_ShouldReturnSuccess()
        {
            // Arrange
            var expectedResponse = new ReportingAPIResponse { Success = true };
            _runtimeApiClientMock
                .Setup(r => r.GetConfigVersion(It.IsAny<string>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _zenApi.Runtime.GetConfigVersion("token");

            // Assert
            Assert.AreEqual(expectedResponse, result);
        }

        [Test]
        public void RuntimeApiClient_GetConfigVersion_ShouldThrowExceptionOnError()
        {
            // Arrange
            _runtimeApiClientMock
                .Setup(r => r.GetConfigVersion(It.IsAny<string>()))
                .ThrowsAsync(new Exception("An error occurred while reporting"));

            // Act & Assert
            Assert.ThrowsAsync<Exception>(async () => await _zenApi.Runtime.GetConfigVersion("token"));
        }

        [Test]
        public async Task RuntimeApiClient_GetConfig_ShouldReturnSuccess()
        {
            // Arrange
            var expectedResponse = new ReportingAPIResponse { Success = true };
            _runtimeApiClientMock
                .Setup(r => r.GetConfig(It.IsAny<string>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _zenApi.Runtime.GetConfig("token");

            // Assert
            Assert.AreEqual(expectedResponse, result);
        }

        [Test]
        public void RuntimeApiClient_GetConfig_ShouldThrowExceptionOnError()
        {
            // Arrange
            _runtimeApiClientMock
                .Setup(r => r.GetConfig(It.IsAny<string>()))
                .ThrowsAsync(new Exception("An error occurred while reporting"));

            // Act & Assert
            Assert.ThrowsAsync<Exception>(async () => await _zenApi.Runtime.GetConfig("token"));
        }

        [Test]
        public async Task ReportingApiClient_GetBlockedIps_ShouldReturnSuccess()
        {
            // Arrange
            var expectedResponse = new BlockedIpsAPIResponse { Success = true };
            _reportingApiClientMock
                .Setup(r => r.GetBlockedIps(It.IsAny<string>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _zenApi.Reporting.GetBlockedIps("token");

            // Assert
            Assert.AreEqual(expectedResponse, result);
        }

        [Test]
        public void ReportingApiClient_GetBlockedIps_ShouldThrowExceptionOnError()
        {
            // Arrange
            _reportingApiClientMock
                .Setup(r => r.GetBlockedIps(It.IsAny<string>()))
                .ThrowsAsync(new Exception("An error occurred while reporting"));

            // Act & Assert
            Assert.ThrowsAsync<Exception>(async () => await _zenApi.Reporting.GetBlockedIps("token"));
        }
    }
}

