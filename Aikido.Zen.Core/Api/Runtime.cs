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
                catch (TaskCanceledException)
                {
                    LogHelper.ErrorLog(Agent.Logger, "Error retrieving config: Operation canceled");
                    return new ConfigLastUpdatedAPIResponse { Success = false, Error = "cancelation" };
                }
                catch (Exception ex)
                {
                    LogHelper.ErrorLog(Agent.Logger, $"Error retrieving config: {ex.Message}");
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
                catch (TaskCanceledException)
                {
                    if (!cts.Token.IsCancellationRequested)
                        return new ReportingAPIResponse { Success = false, Error = "timeout" };

                    throw;
                }
                catch (Exception ex)
                {
                    throw new Exception("An error occurred while reporting", ex);
                }
            }
        }
    }
}
