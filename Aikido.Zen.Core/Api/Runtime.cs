using System;
using System.Net.Http;
using System.Threading.Tasks;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Core.Api
{
    internal class RuntimeAPIClient : IRuntimeAPIClient
    {
        private readonly HttpClient _httpClient;

        public RuntimeAPIClient(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }


        public async Task<ConfigLastUpdatedAPIResponse> GetConfigLastUpdated(string token)
        {
            try
            {
                var request = APIHelper.CreateRequest(token, new Uri(EnvironmentHelper.AikidoRealtimeUrl), "config", HttpMethod.Get);
                var response = await _httpClient.SendAsync(request);
                return APIHelper.ToAPIResponse<ConfigLastUpdatedAPIResponse>(response);
            }
            catch (TaskCanceledException ex)
            {
                LogHelper.ErrorLog(Agent.Logger, $"Error retrieving config last updated (possible timeout): {ex.Message}");
                return new ConfigLastUpdatedAPIResponse { Success = false, Error = "timeout" };
            }
            catch (Exception ex)
            {
                LogHelper.ErrorLog(Agent.Logger, $"Error retrieving config last updated: {ex.Message}");
                return new ConfigLastUpdatedAPIResponse { Success = false, Error = "unknown_error" };
            }
        }

        public async Task<ReportingAPIResponse> GetConfig(string token)
        {
            try
            {
                var request = APIHelper.CreateRequest(token, new Uri(EnvironmentHelper.AikidoUrl), "/api/runtime/config", HttpMethod.Get);
                var response = await _httpClient.SendAsync(request);
                return APIHelper.ToAPIResponse<ReportingAPIResponse>(response);
            }
            catch (TaskCanceledException ex)
            {
                LogHelper.ErrorLog(Agent.Logger, $"Error retrieving config (possible timeout): {ex.Message}");
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
