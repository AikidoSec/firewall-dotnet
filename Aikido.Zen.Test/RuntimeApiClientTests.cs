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
            Task.Delay(100);

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
        public void GetConfigLastUpdated_ShouldContinueOnCanceledTaskCanceledException()
        {
            // Arrange
            _handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .Throws(new TaskCanceledException("Canceled"));

            // Act & Assert
            Assert.DoesNotThrowAsync(async () => await _runtimeApiClient.GetConfigLastUpdated("token"), "Failed: Task Canceled, but the exception propagated.");
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
                .Returns(async (HttpRequestMessage req, CancellationToken token) =>
                {
                    // Simulate waiting for longer than the cancellation timeout
                    await Task.Delay(10000, token);
                    return new HttpResponseMessage();
                });

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            ConfigLastUpdatedAPIResponse result = new();

            // Act

            Assert.DoesNotThrowAsync(async () => result = await _runtimeApiClient.GetConfigLastUpdated("token"), "Failed: Task canceled, but the exception propagated.");

            stopwatch.Stop();

            // Assert
            Assert.That(result!.Success, Is.False);
            Assert.That(stopwatch.ElapsedMilliseconds, Is.InRange(4500, 7000));
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
        public void GetConfig_ShouldThrowExceptionOnError()
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

            // Act & Assert
            Assert.ThrowsAsync<Exception>(async () => await _runtimeApiClient.GetConfig("token"));
        }
    }
}
