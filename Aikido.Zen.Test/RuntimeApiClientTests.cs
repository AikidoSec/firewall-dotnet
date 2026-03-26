using Aikido.Zen.Core.Api;
using Moq;
using Moq.Protected;
using NUnit.Framework;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Aikido.Zen.Test
{
    public class RuntimeApiClientTests
    {
        private Mock<HttpMessageHandler> _handlerMock;
        private RuntimeAPIClient _runtimeApiClient;

        [SetUp]
        public void Setup()
        {
            _handlerMock = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(_handlerMock.Object);
            httpClient.Timeout = TimeSpan.FromMilliseconds(5000);
            _runtimeApiClient = new RuntimeAPIClient(httpClient);
        }

        [Test]
        public async Task GetConfigLastUpdated_ShouldReturnSuccess()
        {
            // Arrange
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"success\":true}")
            };

            _handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(response);

            // Act
            var result = await _runtimeApiClient.GetConfigLastUpdated("token");
            await Task.Delay(100);

            // Assert
            Assert.That(result.Success);
            _handlerMock.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri.PathAndQuery.Contains("config")),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Test]
        public void GetConfigLastUpdated_ShouldNotThrowExceptionOnError()
        {
            // Arrange
            _handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ThrowsAsync(new Exception("An error occurred while getting config version"));

            // Act & Assert
            Assert.DoesNotThrowAsync(async () => await _runtimeApiClient.GetConfigLastUpdated("token"));
        }

        [Test]
        public async Task GetConfigLastUpdated_ShouldContinueOnTimeoutTaskCanceledException()
        {
            // Arrange
            _handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ThrowsAsync(new TaskCanceledException("Timed out", new TimeoutException()));

            ConfigLastUpdatedAPIResponse result = new();

            // Act

            Assert.DoesNotThrowAsync(async () => result = await _runtimeApiClient.GetConfigLastUpdated("token"), "Failed: Task timed out, but the exception propagated.");

            // Assert
            Assert.That(result!.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo("timeout"));
        }

        [Test]
        public async Task GetConfig_ShouldReturnSuccess()
        {
            // Arrange
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"success\":true}")
            };

            _handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(response);

            // Act
            var result = await _runtimeApiClient.GetConfig("token");
            await Task.Delay(100);

            // Assert
            Assert.That(result.Success);
            _handlerMock.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri.PathAndQuery.Contains("/api/runtime/config")),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Test]
        public async Task GetConfig_ShouldReturnUnknownErrorOnError()
        {
            // Arrange
            _handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ThrowsAsync(new Exception("An error occurred while getting config"));

            ReportingAPIResponse result = new();

            // Act
            Assert.DoesNotThrowAsync(async () => result = await _runtimeApiClient.GetConfig("token"));

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo("unknown_error"));
        }

        [Test]
        public async Task GetConfig_ShouldReturnTimeoutOnTimeoutTaskCanceledException()
        {
            // Arrange
            _handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ThrowsAsync(new TaskCanceledException("Timed out", new TimeoutException()));

            ReportingAPIResponse result = new();

            // Act
            Assert.DoesNotThrowAsync(async () => result = await _runtimeApiClient.GetConfig("token"));

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo("timeout"));
        }

    }
}
