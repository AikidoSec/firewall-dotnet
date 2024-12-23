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
        public async Task GetConfigVersion_ShouldReturnSuccess()
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
            var result = await _runtimeApiClient.GetConfigVersion("token");
            Task.Delay(100);

            // Assert
            Assert.IsTrue(result.Success);
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
        public void GetConfigVersion_ShouldThrowExceptionOnError()
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
            Assert.ThrowsAsync<Exception>(async () => await _runtimeApiClient.GetConfigVersion("token"));
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
            Assert.IsTrue(result.Success);
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
