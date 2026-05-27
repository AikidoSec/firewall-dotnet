using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
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

        public async Task<ReportingAPIResponse> ReportAsync(string token, object @event, CancellationToken cancellationToken)
        {
            try
            {
                // make sure the json string does not use unicode the escape characters
                var eventAsJson = JsonSerializer.Serialize(@event, ZenApi.JsonSerializerOptions);
                var requestContent = new StringContent(eventAsJson, Encoding.UTF8, "application/json");
                var request = APIHelper.CreateRequest(token, new Uri(EnvironmentHelper.AikidoUrl), "api/runtime/events", HttpMethod.Post, requestContent);
                var response = await _httpClient.SendAsync(request, cancellationToken);
                return APIHelper.ToAPIResponse<ReportingAPIResponse>(response);
            }
            catch (TaskCanceledException ex)
            {
                LogHelper.WarningLog(Agent.Logger, $"Failed to report event (possible timeout): {ex.Message}");
                return new ReportingAPIResponse { Success = false, Error = "timeout" };
            }
            catch (Exception ex)
            {
                LogHelper.WarningLog(Agent.Logger, $"Failed to report event: {ex.Message}");
                return new ReportingAPIResponse { Success = false, Error = "unknown_error" };
            }
        }

        public async Task<FirewallListsAPIResponse> GetFirewallLists(string token, CancellationToken cancellationToken)
        {
            try
            {
                var request = APIHelper.CreateRequest(token, new Uri(EnvironmentHelper.AikidoUrl), "api/runtime/firewall/lists", HttpMethod.Get);
                var response = await _httpClient.SendAsync(request, cancellationToken);
                return APIHelper.ToAPIResponse<FirewallListsAPIResponse>(response);
            }
            catch (TaskCanceledException ex)
            {
                LogHelper.DebugLog(Agent.Logger, $"Retrieving Firewall Lists timed out; will retry on a future config update: {ex.Message}");
                return new FirewallListsAPIResponse { Success = false, Error = "timeout" };
            }
            catch (Exception ex)
            {
                LogHelper.WarningLog(Agent.Logger, $"Failed to retrieve Firewall Lists: {ex.Message}");
                return new FirewallListsAPIResponse { Success = false, Error = "unknown_error" };
            }
        }
    }
}
