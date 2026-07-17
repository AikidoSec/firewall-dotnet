using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Realtime;
using Moq;
using NUnit.Framework;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Aikido.Zen.Test
{
    public class RealtimeConfigUpdateListenerTests
    {
        [TestCase(HttpStatusCode.Unauthorized)]
        [TestCase(HttpStatusCode.Forbidden)]
        public async Task RunAsync_RetriesTransientFailuresAndStopsOnRejectedToken(
            HttpStatusCode rejectedStatus)
        {
            var runtimeApi = new Mock<IRuntimeAPIClient>();
            var calls = 0;
            runtimeApi
                .Setup(api => api.SubscribeToConfigUpdates(
                    It.IsAny<string>(),
                    It.IsAny<Func<long, Task>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    calls++;
                    return calls == 1
                        ? Task.FromException<HttpStatusCode>(new HttpRequestException("temporary failure"))
                        : Task.FromResult(rejectedStatus);
                });

            await RealtimeConfigUpdateListener.RunAsync(
                runtimeApi.Object,
                "test-token",
                _ => Task.CompletedTask,
                CancellationToken.None,
                initialReconnectDelay: TimeSpan.FromMilliseconds(1));

            Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public async Task RunAsync_ForwardsUpdatesAndStopsWhenCancelled()
        {
            var subscribed = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var runtimeApi = new Mock<IRuntimeAPIClient>();
            runtimeApi
                .Setup(api => api.SubscribeToConfigUpdates(
                    It.IsAny<string>(),
                    It.IsAny<Func<long, Task>>(),
                    It.IsAny<CancellationToken>()))
                .Returns<string, Func<long, Task>, CancellationToken>(
                    async (_, onUpdate, cancellationToken) =>
                    {
                        await onUpdate(321);
                        subscribed.TrySetResult(true);
                        await Task.Delay(-1, cancellationToken);
                        return HttpStatusCode.OK;
                    });

            long received = 0;
            using var cancellationSource = new CancellationTokenSource();
            var listenerTask = RealtimeConfigUpdateListener.RunAsync(
                runtimeApi.Object,
                "test-token",
                configUpdatedAt =>
                {
                    received = configUpdatedAt;
                    return Task.CompletedTask;
                },
                cancellationSource.Token);

            await subscribed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellationSource.Cancel();
            await listenerTask.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.That(received, Is.EqualTo(321));
        }

        [Test]
        public async Task RunAsync_StopsWhenCancelledDuringReconnectDelay()
        {
            var disconnected = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var runtimeApi = new Mock<IRuntimeAPIClient>();
            runtimeApi
                .Setup(api => api.SubscribeToConfigUpdates(
                    It.IsAny<string>(),
                    It.IsAny<Func<long, Task>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    disconnected.TrySetResult(true);
                    return Task.FromResult(HttpStatusCode.OK);
                });

            using var cancellationSource = new CancellationTokenSource();
            var listenerTask = RealtimeConfigUpdateListener.RunAsync(
                runtimeApi.Object,
                "test-token",
                _ => Task.CompletedTask,
                cancellationSource.Token,
                initialReconnectDelay: TimeSpan.FromSeconds(30));

            await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellationSource.Cancel();
            await listenerTask.WaitAsync(TimeSpan.FromSeconds(5));

            runtimeApi.Verify(
                api => api.SubscribeToConfigUpdates(
                    It.IsAny<string>(),
                    It.IsAny<Func<long, Task>>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
