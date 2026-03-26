using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Core.Api
{
    internal class ReportingAPIClient : IReportingAPIClient
    {
        private readonly HttpClient _httpClient;

        public ReportingAPIClient(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public async Task<ReportingAPIResponse> ReportAsync(string token, object @event)
        {
            try
            {
                // make sure the json string does not use unicode the escape characters
                var eventAsJson = JsonSerializer.Serialize(@event, ZenApi.JsonSerializerOptions);
                var requestContent = new StringContent(eventAsJson, Encoding.UTF8, "application/json");
                var request = APIHelper.CreateRequest(token, new Uri(EnvironmentHelper.AikidoUrl), "api/runtime/events", HttpMethod.Post, requestContent);
                var response = await _httpClient.SendAsync(request);
                return APIHelper.ToAPIResponse<ReportingAPIResponse>(response);
            }
            catch (TaskCanceledException ex)
            {
                LogHelper.ErrorLog(Agent.Logger, $"Error reporting event (possible timeout): {ex.Message}");
                return new ReportingAPIResponse { Success = false, Error = "timeout" };
            }
            catch (Exception ex)
            {
                LogHelper.ErrorLog(Agent.Logger, $"Error reporting event: {ex.Message}");
                return new ReportingAPIResponse { Success = false, Error = "unknown_error" };
            }
        }

        public async Task<FirewallListsAPIResponse> GetFirewallLists(string token)
        {
            try
            {
                var request = APIHelper.CreateRequest(token, new Uri(EnvironmentHelper.AikidoUrl), "api/runtime/firewall/lists", HttpMethod.Get);
                var response = await _httpClient.SendAsync(request);
                return APIHelper.ToAPIResponse<FirewallListsAPIResponse>(response);
            }
            catch (TaskCanceledException ex)
            {
                LogHelper.ErrorLog(Agent.Logger, $"Error retrieving Firewall Lists (possible timeout): {ex.Message}");
                return new FirewallListsAPIResponse { Success = false, Error = "timeout" };
            }
            catch (Exception ex)
            {
                LogHelper.ErrorLog(Agent.Logger, $"Error retrieving Firewall Lists: {ex.Message}");
                return new FirewallListsAPIResponse { Success = false, Error = "unknown_error" };
            }
        }
    }
}
