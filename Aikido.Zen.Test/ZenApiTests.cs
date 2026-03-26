using Aikido.Zen.Core.Api;
using Moq;
using Aikido.Zen.Tests.Mocks;

namespace Aikido.Zen.Test
{
    public class ZenApiTests
    {
        private IZenApi _zenApi = null!;

        [Test]
        public async Task ReportingApiClient_ReportAsync_ShouldReturnSuccess()
        {
            // Arrange
            _zenApi = ZenApiMock.CreateMock().Object;

            // Act
            var result = await _zenApi.Reporting.ReportAsync("token", new { });

            // Assert
            Assert.That(result.Success);
        }

        [Test]
        public void ReportingApiClient_ReportAsync_ShouldThrowExceptionOnError()
        {
            // Arrange
            _zenApi = ZenApiMock.CreateMockWithExceptions().Object;

            // Act & Assert
            Assert.ThrowsAsync<Exception>(async () => await _zenApi.Reporting.ReportAsync("token", new { }));
        }

        [Test]
        public async Task ZenApiMock_WithFailedResponses_ShouldReturnFailedResponses()
        {
            // Arrange
            _zenApi = ZenApiMock.CreateMockWithFailedResponses().Object;

            // Act
            var reportResponse = await _zenApi.Reporting.ReportAsync("token", new { });
            var firewallListsResponse = await _zenApi.Reporting.GetFirewallLists("token");
            var configVersionResponse = await _zenApi.Runtime.GetConfigLastUpdated("token");
            var configResponse = await _zenApi.Runtime.GetConfig("token");

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(reportResponse.Success, Is.False);
                Assert.That(firewallListsResponse.Success, Is.False);
                Assert.That(configVersionResponse.Success, Is.False);
                Assert.That(configResponse.Success, Is.False);
            });
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
            var result = await _zenApi.Reporting.GetFirewallLists("token");

            // Assert
            Assert.That(result.Success);
        }

        [Test]
        public void ReportingApiClient_GetBlockedIps_ShouldThrowExceptionOnError()
        {
            // Arrange
            _zenApi = ZenApiMock.CreateMockWithExceptions().Object;

            // Act & Assert
            Assert.ThrowsAsync<Exception>(async () => await _zenApi.Reporting.GetFirewallLists("token"));
        }
    }
}
