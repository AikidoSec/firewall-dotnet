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
            var result = await _reportingApiClient.ReportAsync("token", new { }, 5000);

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
            Assert.DoesNotThrowAsync(async () => await _reportingApiClient.ReportAsync("token", new { }, 5000));
        }
        [Test]
        public void ReportAsync_ShouldContinueOnCanceledTaskCanceledException()
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
            Assert.DoesNotThrowAsync(async () => await _reportingApiClient.ReportAsync("token", new {}, 5000), "Failed: Task Canceled, but the exception propagated.");
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
                .Returns(async (HttpRequestMessage req, CancellationToken token) =>
                {
                    // Simulate waiting for longer than the cancellation timeout
                    await Task.Delay(10000, token);
                    return new HttpResponseMessage();
                });

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            ReportingAPIResponse result = new();

            // Act

            Assert.DoesNotThrowAsync(async () => result = await _reportingApiClient.ReportAsync("token", new { }, 5000), "Failed: Task timed out, but the exception propagated.");

            stopwatch.Stop();

            // Assert
            Assert.That(result!.Success, Is.False);
            Assert.That(stopwatch.ElapsedMilliseconds, Is.InRange(4500, 7000));
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
        public void GetFirewallLists_ShouldThrowExceptionOnError()
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
            Assert.ThrowsAsync<Exception>(async () => await _reportingApiClient.GetFirewallLists("token"));
        }
        [Test]
        public void GetFirewallLists_ShouldContinueOnCanceledTaskCanceledException()
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
            Assert.DoesNotThrowAsync(async () => await _reportingApiClient.GetFirewallLists("token"),"Failed: Task Canceled, but the exception propagated.");
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
                .Returns(async (HttpRequestMessage req, CancellationToken token) =>
                {
                    // Simulate waiting for longer than the cancellation timeout
                    await Task.Delay(10000, token); 
                    return new HttpResponseMessage();
                });

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            FirewallListsAPIResponse result = new ();

            // Act

            Assert.DoesNotThrowAsync(async () => result = await _reportingApiClient.GetFirewallLists("token"), "Failed: Task timed out, but the exception propagated.");

            stopwatch.Stop();

            // Assert
            Assert.That(result!.Success, Is.False);
            Assert.That(stopwatch.ElapsedMilliseconds, Is.InRange( 4500, 7000));
        }
    }
}
