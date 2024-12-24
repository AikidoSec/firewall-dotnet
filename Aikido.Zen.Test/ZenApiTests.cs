using Aikido.Zen.Core.Api;
using Moq;
using Aikido.Zen.Test.Mocks;

namespace Aikido.Zen.Test
{
    public class ZenApiTests
    {
        private IZenApi _zenApi;

        [Test]
        public async Task ReportingApiClient_ReportAsync_ShouldReturnSuccess()
        {
            // Arrange
            _zenApi = ZenApiMock.CreateMock().Object;

            // Act
            var result = await _zenApi.Reporting.ReportAsync("token", new { }, 5000);

            // Assert
            Assert.That(result.Success);
        }

        [Test]
        public void ReportingApiClient_ReportAsync_ShouldThrowExceptionOnError()
        {
            // Arrange
            _zenApi = ZenApiMock.CreateMockWithExceptions().Object;

            // Act & Assert
            Assert.ThrowsAsync<Exception>(async () => await _zenApi.Reporting.ReportAsync("token", new { }, 5000));
        }

        [Test]
        public async Task RuntimeApiClient_GetConfigVersion_ShouldReturnSuccess()
        {
            // Arrange
            _zenApi = ZenApiMock.CreateMock().Object;

            // Act
            var result = await _zenApi.Runtime.GetConfigLastUpdated("token");

            // Assert
            Assert.That(result.Success);
        }

        [Test]
        public void RuntimeApiClient_GetConfigVersion_ShouldThrowExceptionOnError()
        {
            // Arrange
            _zenApi = ZenApiMock.CreateMockWithExceptions().Object;

            // Act & Assert
            Assert.ThrowsAsync<Exception>(async () => await _zenApi.Runtime.GetConfigLastUpdated("token"));
        }

        [Test]
        public async Task RuntimeApiClient_GetConfig_ShouldReturnSuccess()
        {
            // Arrange
            _zenApi = ZenApiMock.CreateMock().Object;

            // Act
            var result = await _zenApi.Runtime.GetConfig("token");

            // Assert
            Assert.That(result.Success);
        }

        [Test]
        public void RuntimeApiClient_GetConfig_ShouldThrowExceptionOnError()
        {
            // Arrange
            _zenApi = ZenApiMock.CreateMockWithExceptions().Object;

            // Act & Assert
            Assert.ThrowsAsync<Exception>(async () => await _zenApi.Runtime.GetConfig("token"));
        }

        [Test]
        public async Task ReportingApiClient_GetBlockedIps_ShouldReturnSuccess()
        {
            // Arrange
            _zenApi = ZenApiMock.CreateMock().Object;

            // Act
            var result = await _zenApi.Reporting.GetBlockedIps("token");

            // Assert
            Assert.That(result.Success);
        }

        [Test]
        public void ReportingApiClient_GetBlockedIps_ShouldThrowExceptionOnError()
        {
            // Arrange
            _zenApi = ZenApiMock.CreateMockWithExceptions().Object;

            // Act & Assert
            Assert.ThrowsAsync<Exception>(async () => await _zenApi.Reporting.GetBlockedIps("token"));
        }
    }
}

