using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Core.Realtime
{
    internal sealed class RealtimeConfigUpdateListener
    {
        private static readonly TimeSpan InitialReconnectDelay = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan MaxReconnectDelay = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan StableConnectionThreshold = TimeSpan.FromSeconds(30);

        internal TimeSpan ReconnectDelay { get; set; } = InitialReconnectDelay;
        internal DateTime ConnectionStartedAt { get; set; }

        internal async Task RunAsync(
            IRuntimeAPIClient runtimeApi,
            string token,
            Func<long, Task> onUpdate,
            CancellationToken cancellationToken)
        {
            var random = new Random();

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    ConnectionStartedAt = DateTime.UtcNow;

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

                    if (DateTime.UtcNow - ConnectionStartedAt >= StableConnectionThreshold)
                    {
                        ReconnectDelay = InitialReconnectDelay;
                    }

                    var jitter = TimeSpan.FromMilliseconds(
                        ReconnectDelay.TotalMilliseconds * random.NextDouble() / 2);
                    await Task.Delay(ReconnectDelay + jitter, cancellationToken).ConfigureAwait(false);

                    ReconnectDelay = TimeSpan.FromTicks(Math.Min(
                        ReconnectDelay.Ticks * 2,
                        MaxReconnectDelay.Ticks));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }
    }
}
