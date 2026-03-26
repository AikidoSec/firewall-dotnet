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
    public class ReportingAPiClientTests
    {
        private Mock<HttpMessageHandler> _handlerMock;
        private ReportingAPIClient _reportingApiClient;

        [SetUp]
        public void Setup()
        {
            _handlerMock = new Mock<HttpMessageHandler>();
            var httpClient = new HttpClient(_handlerMock.Object);
            httpClient.Timeout = TimeSpan.FromMilliseconds(5000);

            // set the handler to the http client
            _reportingApiClient = new ReportingAPIClient(httpClient);
        }

        [Test]
        public async Task ReportAsync_ShouldReturnSuccess()
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
            var result = await _reportingApiClient.ReportAsync("token", new { });

            // Assert
            Assert.That(result.Success);
            _handlerMock.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri.PathAndQuery.Contains("api/runtime/events")),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Test]
        public void ReportAsync_ShouldNotThrowExceptionOnError()
        {
            // Arrange
            _handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ThrowsAsync(new Exception("An error occurred while reporting"));

            // Act & Assert
            Assert.DoesNotThrowAsync(async () => await _reportingApiClient.ReportAsync("token", new { }));
        }
        [Test]
        public async Task ReportAsync_ShouldContinueOnTimeoutTaskCanceledException()
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

            Assert.DoesNotThrowAsync(async () => result = await _reportingApiClient.ReportAsync("token", new { }), "Failed: Task timed out, but the exception propagated.");

            // Assert
            Assert.That(result!.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo("timeout"));
        }

        [Test]
        public async Task GetFirewallLists_ShouldReturnSuccess()
        {
            // Arrange
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"success\":true, \"blockedIPAddresses\":[]}")
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
            var result = await _reportingApiClient.GetFirewallLists("token");
            await Task.Delay(100);

            // Assert
            Assert.That(result.Success);
            _handlerMock.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri.PathAndQuery.Contains("api/runtime/firewall/lists")),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Test]
        public void GetFirewallLists_ShouldNotThrowExceptionOnError()
        {
            // Arrange
            _handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ThrowsAsync(new Exception("An error occurred while getting blocked IPs"));

            // Act & Assert
            Assert.DoesNotThrowAsync(async () => await _reportingApiClient.GetFirewallLists("token"));
        }
        [Test]
        public async Task GetFirewallLists_ShouldContinueOnTimeoutTaskCanceledException()
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

            FirewallListsAPIResponse result = new();

            // Act

            Assert.DoesNotThrowAsync(async () => result = await _reportingApiClient.GetFirewallLists("token"), "Failed: Task timed out, but the exception propagated.");

            // Assert
            Assert.That(result!.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo("timeout"));
        }

    }
}
