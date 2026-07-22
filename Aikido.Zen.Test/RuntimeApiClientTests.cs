using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Helpers;
using Moq;
using Moq.Protected;
using NUnit.Framework;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
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
            var result = await _runtimeApiClient.GetConfigLastUpdated("token", CancellationToken.None);
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
            Assert.DoesNotThrowAsync(async () => await _runtimeApiClient.GetConfigLastUpdated("token", CancellationToken.None));
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

            Assert.DoesNotThrowAsync(async () => result = await _runtimeApiClient.GetConfigLastUpdated("token", CancellationToken.None), "Failed: Task timed out, but the exception propagated.");

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
            var result = await _runtimeApiClient.GetConfig("token", CancellationToken.None);
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
            Assert.DoesNotThrowAsync(async () => result = await _runtimeApiClient.GetConfig("token", CancellationToken.None));

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
            Assert.DoesNotThrowAsync(async () => result = await _runtimeApiClient.GetConfig("token", CancellationToken.None));

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo("timeout"));
        }

        [Test]
        public async Task SubscribeToConfigUpdates_ShouldReadConfigUpdatedEvents()
        {
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    "\uFEFF: ping\r\n\r\n" +
                    "retry\r\n" +
                    "event: config-updated\r\n" +
                    "data: {\r\n" +
                    "data: \"configUpdatedAt\": 123\r\n" +
                    "data: }\r\n\r\n",
                    Encoding.UTF8,
                    "text/event-stream")
            };

            _handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            long configUpdatedAt = 0;
            using var cancellationSource = new CancellationTokenSource();

            var statusCode = await _runtimeApiClient.SubscribeToConfigUpdates(
                "test-token",
                value =>
                {
                    configUpdatedAt = value;
                    cancellationSource.Cancel();
                    return Task.CompletedTask;
                },
                cancellationSource.Token);

            Assert.That(statusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(configUpdatedAt, Is.EqualTo(123L));
            _handlerMock.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(request =>
                    request.Method == HttpMethod.Get &&
                    request.RequestUri!.AbsolutePath == "/api/runtime/stream" &&
                    request.Headers.Authorization!.Scheme == "test-token" &&
                    request.Headers.Accept.ToString() == "text/event-stream" &&
                    !request.Headers.AcceptEncoding.Any() &&
                    request.Headers.CacheControl!.NoCache &&
                    request.Headers.GetValues("X-Agent-Platform").Single() == "dotnet" &&
                    request.Headers.GetValues("X-Agent-Version").Single() == AgentInfoHelper.ZenVersion),
                ItExpr.IsAny<CancellationToken>());
        }

        [Test]
        public async Task SubscribeToConfigUpdates_ShouldIgnoreInvalidPayloads()
        {
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(
                    "event: config-updated\n" +
                    "data: not-json\n\n" +
                    "event: config-updated\n" +
                    "data: {\"configUpdatedAt\":456}\n\n",
                    Encoding.UTF8,
                    "text/event-stream")
            };

            _handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);

            long configUpdatedAt = 0;

            var statusCode = await _runtimeApiClient.SubscribeToConfigUpdates(
                "test-token",
                value =>
                {
                    configUpdatedAt = value;
                    return Task.CompletedTask;
                },
                CancellationToken.None);

            Assert.That(statusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(configUpdatedAt, Is.EqualTo(456L));
        }

        [TestCase(HttpStatusCode.Unauthorized)]
        [TestCase(HttpStatusCode.Forbidden)]
        public async Task SubscribeToConfigUpdates_ShouldReturnRejectedStatus(HttpStatusCode statusCode)
        {
            _handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage { StatusCode = statusCode });

            var result = await _runtimeApiClient.SubscribeToConfigUpdates(
                "test-token",
                _ => Task.CompletedTask,
                CancellationToken.None);

            Assert.That(result, Is.EqualTo(statusCode));
        }

        [Test]
        public void WaitForLineOrTimeout_ShouldStopWhenCancelled()
        {
            var readTask = new TaskCompletionSource<string>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using var cancellationSource = new CancellationTokenSource();
            cancellationSource.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(() =>
                RuntimeAPIClient.WaitForLineOrTimeout(
                    readTask.Task,
                    cancellationSource.Token,
                    TimeSpan.FromSeconds(1)));
        }

        [Test]
        public void WaitForLineOrTimeout_ShouldThrowAfterTimeout()
        {
            var readTask = new TaskCompletionSource<string>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            Assert.ThrowsAsync<TimeoutException>(() =>
                RuntimeAPIClient.WaitForLineOrTimeout(
                    readTask.Task,
                    CancellationToken.None,
                    TimeSpan.Zero));
        }

    }
}
