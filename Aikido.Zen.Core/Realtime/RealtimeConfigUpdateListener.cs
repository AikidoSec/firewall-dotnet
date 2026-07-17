using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Core.Realtime
{
    internal static class RealtimeConfigUpdateListener
    {
        private static readonly TimeSpan MaxReconnectDelay = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan StableConnectionThreshold = TimeSpan.FromSeconds(30);

        internal static async Task RunAsync(
            IRuntimeAPIClient runtimeApi,
            string token,
            Func<long, Task> onUpdate,
            CancellationToken cancellationToken,
            TimeSpan? initialReconnectDelay = null)
        {
            var initialDelay = initialReconnectDelay ?? TimeSpan.FromSeconds(5);
            var reconnectDelay = initialDelay;
            var random = new Random();

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var connectedAt = DateTime.UtcNow;

                    try
                    {
                        var statusCode = await runtimeApi.SubscribeToConfigUpdates(
                            token,
                            onUpdate,
                            cancellationToken).ConfigureAwait(false);

                        if (statusCode == HttpStatusCode.Unauthorized ||
                            statusCode == HttpStatusCode.Forbidden)
                        {
                            LogHelper.WarningLog(
                                Agent.Logger,
                                $"Realtime config connection rejected with status {(int)statusCode}; stopping");
                            return;
                        }

                        LogHelper.DebugLog(Agent.Logger, "Realtime config connection closed; reconnecting");
                    }
                    catch (Exception ex)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }

                        LogHelper.DebugLog(
                            Agent.Logger,
                            ex,
                            "Realtime config connection failed; reconnecting");
                    }

                    if (DateTime.UtcNow - connectedAt >= StableConnectionThreshold)
                    {
                        reconnectDelay = initialDelay;
                    }

                    var jitter = TimeSpan.FromMilliseconds(
                        reconnectDelay.TotalMilliseconds * random.NextDouble() / 2);
                    await Task.Delay(reconnectDelay + jitter, cancellationToken).ConfigureAwait(false);

                    reconnectDelay = TimeSpan.FromTicks(Math.Min(
                        reconnectDelay.Ticks * 2,
                        MaxReconnectDelay.Ticks));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }
    }
}
