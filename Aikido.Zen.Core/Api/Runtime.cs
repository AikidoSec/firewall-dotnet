using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Core.Api
{
    internal class RuntimeAPIClient : IRuntimeAPIClient
    {
        private readonly HttpClient _httpClient;
        private readonly int _timeoutInMS;

        public RuntimeAPIClient(HttpClient httpClient, int timeoutInMS = 30000)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _timeoutInMS = timeoutInMS;
        }


        public async Task<ConfigLastUpdatedAPIResponse> GetConfigLastUpdated(string token)
        {
            using (var cts = new CancellationTokenSource(_timeoutInMS))
            {
                try
                {
                    var request = APIHelper.CreateRequest(token, new Uri(EnvironmentHelper.AikidoRealtimeUrl), "config", HttpMethod.Get);
                    var response = await _httpClient.SendAsync(request, cts.Token);
                    return APIHelper.ToAPIResponse<ConfigLastUpdatedAPIResponse>(response);
                }
                catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
                {
                    LogHelper.ErrorLog(Agent.Logger, $"Error retrieving config last updated (timeout): {ex.Message}");
                    return new ConfigLastUpdatedAPIResponse { Success = false, Error = "timeout" };
                }
                catch (Exception ex)
                {
                    LogHelper.ErrorLog(Agent.Logger, $"Error retrieving config last updated: {ex.Message}");
                    return new ConfigLastUpdatedAPIResponse { Success = false, Error = "unknown_error" };
                }
            }
        }

        public async Task<ReportingAPIResponse> GetConfig(string token)
        {
            using (var cts = new CancellationTokenSource(_timeoutInMS))
            {
                var request = APIHelper.CreateRequest(token, new Uri(EnvironmentHelper.AikidoUrl), "/api/runtime/config", HttpMethod.Get);

                try
                {
                    var response = await _httpClient.SendAsync(request, cts.Token);
                    return APIHelper.ToAPIResponse<ReportingAPIResponse>(response);
                }
                catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
                {
                    LogHelper.ErrorLog(Agent.Logger, $"Error retrieving config (timeout): {ex.Message}");
                    return new ReportingAPIResponse { Success = false, Error = "timeout" };
                }
                catch (Exception ex)
                {
                    LogHelper.ErrorLog(Agent.Logger, $"Error retrieving config: {ex.Message}");
                    return new ReportingAPIResponse { Success = false, Error = "unknown_error" };
                }
            }
        }
    }
}
