using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Realtime;

namespace Aikido.Zen.Core.Api
{
    internal class RuntimeAPIClient : IRuntimeAPIClient
    {
        private static readonly TimeSpan SseReadTimeout = TimeSpan.FromSeconds(70);
        private readonly HttpClient _httpClient;

        public RuntimeAPIClient(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public async Task<ConfigLastUpdatedAPIResponse> GetConfigLastUpdated(string token, CancellationToken cancellationToken)
        {
            try
            {
                var request = APIHelper.CreateRequest(token, new Uri(EnvironmentHelper.AikidoRealtimeUrl), "config", HttpMethod.Get);
                var response = await _httpClient.SendAsync(request, cancellationToken);
                return APIHelper.ToAPIResponse<ConfigLastUpdatedAPIResponse>(response);
            }
            catch (TaskCanceledException ex)
            {
                LogHelper.WarningLog(Agent.Logger, ex, "Failed to retrieve config last updated (possible timeout)");
                return new ConfigLastUpdatedAPIResponse { Success = false, Error = "timeout" };
            }
            catch (Exception ex)
            {
                LogHelper.WarningLog(Agent.Logger, ex, "Failed to retrieve config last updated");
                return new ConfigLastUpdatedAPIResponse { Success = false, Error = "unknown_error" };
            }
        }

        public async Task<ReportingAPIResponse> GetConfig(string token, CancellationToken cancellationToken)
        {
            try
            {
                var request = APIHelper.CreateRequest(token, new Uri(EnvironmentHelper.AikidoUrl), "/api/runtime/config", HttpMethod.Get);
                var response = await _httpClient.SendAsync(request, cancellationToken);
                return APIHelper.ToAPIResponse<ReportingAPIResponse>(response);
            }
            catch (TaskCanceledException ex)
            {
                LogHelper.WarningLog(Agent.Logger, ex, "Failed to retrieve config (possible timeout)");
                return new ReportingAPIResponse { Success = false, Error = "timeout" };
            }
            catch (Exception ex)
            {
                LogHelper.WarningLog(Agent.Logger, ex, "Failed to retrieve config");
                return new ReportingAPIResponse { Success = false, Error = "unknown_error" };
            }
        }

        public async Task<HttpStatusCode> SubscribeToConfigUpdates(
            string token,
            Func<long, Task> onUpdate,
            CancellationToken cancellationToken)
        {
            using (var request = APIHelper.CreateRequest(
                token,
                new Uri(EnvironmentHelper.AikidoRealtimeUrl),
                "/api/runtime/stream",
                HttpMethod.Get))
            {
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
                request.Headers.AcceptEncoding.Clear();
                request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
                request.Headers.Add("X-Agent-Platform", "dotnet");
                request.Headers.Add("X-Agent-Version", AgentInfoHelper.ZenVersion);

                using (var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        return response.StatusCode;
                    }

                    using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var reader = new StreamReader(stream))
                    {
                        var parser = new SseParser();

                        while (!cancellationToken.IsCancellationRequested)
                        {
                            var line = await WaitForLineOrTimeout(
                                reader.ReadLineAsync(),
                                cancellationToken,
                                SseReadTimeout).ConfigureAwait(false);
                            if (line == null)
                            {
                                return response.StatusCode;
                            }

                            if (!parser.TryProcessLine(line, out var eventName, out var data) ||
                                eventName != "config-updated")
                            {
                                continue;
                            }

                            try
                            {
                                var payload = JsonSerializer.Deserialize<ConfigLastUpdatedAPIResponse>(
                                    data,
                                    ZenApi.JsonSerializerOptions);
                                if (payload != null)
                                {
                                    await onUpdate(payload.ConfigUpdatedAt).ConfigureAwait(false);
                                }
                            }
                            catch (JsonException ex)
                            {
                                LogHelper.DebugLog(
                                    Agent.Logger,
                                    ex,
                                    "Ignoring invalid realtime config update payload");
                            }
                        }
                    }

                    return response.StatusCode;
                }
            }
        }

        internal static async Task<string> WaitForLineOrTimeout(
            Task<string> readTask,
            CancellationToken cancellationToken,
            TimeSpan timeout)
        {
            using (var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                var timeoutTask = Task.Delay(timeout, timeoutSource.Token);
                var completedTask = await Task.WhenAny(readTask, timeoutTask).ConfigureAwait(false);

                if (completedTask == readTask)
                {
                    timeoutSource.Cancel();
                    return await readTask.ConfigureAwait(false);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException("Realtime config connection did not receive data before the read timeout");
        }
    }
}
