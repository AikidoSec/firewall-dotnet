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
        private readonly int _timeoutInMS;

        public ReportingAPIClient(HttpClient httpClient, int timeoutInMS = 30000)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _timeoutInMS = timeoutInMS;
        }

        public async Task<ReportingAPIResponse> ReportAsync(string token, object @event)
        {
            using (var cts = new CancellationTokenSource(_timeoutInMS))
            {
                try
                {
                    // make sure the json string does not use unicode the escape characters
                    var eventAsJson = JsonSerializer.Serialize(@event, ZenApi.JsonSerializerOptions);
                    var requestContent = new StringContent(eventAsJson, Encoding.UTF8, "application/json");
                    var request = APIHelper.CreateRequest(token, new Uri(EnvironmentHelper.AikidoUrl), "api/runtime/events", HttpMethod.Post, requestContent);

                    var response = await _httpClient.SendAsync(request, cts.Token);
                    return APIHelper.ToAPIResponse<ReportingAPIResponse>(response);
                }
                catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
                {
                    LogHelper.ErrorLog(Agent.Logger, $"Error reporting event (timeout): {ex.Message}");
                    return new ReportingAPIResponse { Success = false, Error = "timeout" };
                }
                catch (Exception ex)
                {
                    LogHelper.ErrorLog(Agent.Logger, $"Error reporting event: {ex.Message}");
                    return new ReportingAPIResponse { Success = false, Error = "unknown_error" };
                }
            }
        }

        public async Task<FirewallListsAPIResponse> GetFirewallLists(string token)
        {
            using (var cts = new CancellationTokenSource(_timeoutInMS))
            {
                var request = APIHelper.CreateRequest(token, new Uri(EnvironmentHelper.AikidoUrl), "api/runtime/firewall/lists", HttpMethod.Get);
                try
                {
                    var response = await _httpClient.SendAsync(request, cts.Token);
                    return APIHelper.ToAPIResponse<FirewallListsAPIResponse>(response);
                }
                catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
                {
                    LogHelper.ErrorLog(Agent.Logger, $"Error retrieving Firewall Lists (timeout): {ex.Message}");
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
}
