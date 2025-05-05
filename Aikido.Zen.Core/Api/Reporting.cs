using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Core.Api
{
    public class ReportingAPIClient : IReportingAPIClient
    {
        private readonly HttpClient _httpClient;

        public ReportingAPIClient()
        {
            var handler = new HttpClientHandler();
            handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            _httpClient = new HttpClient(handler);
        }

        // used for testing purposes
        public ReportingAPIClient(HttpClient httpClient)
        {
            if (httpClient == null)
            {
                var handler = new HttpClientHandler();
                handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                _httpClient = new HttpClient(handler);
            }
            _httpClient = httpClient;
            httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
            httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
        }

        public async Task<ReportingAPIResponse> ReportAsync(string token, object @event, int timeoutInMS)
        {
            using (var cts = new CancellationTokenSource(timeoutInMS))
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

        public async Task<FirewallListsAPIResponse> GetFirewallLists(string token)
        {
            using (var cts = new CancellationTokenSource(5000))
            {
                var request = APIHelper.CreateRequest(token, new Uri(EnvironmentHelper.AikidoUrl), "api/runtime/firewall/lists", HttpMethod.Get);
                try
                {
                    var response = await _httpClient.SendAsync(request, cts.Token);
                    return APIHelper.ToAPIResponse<FirewallListsAPIResponse>(response);
                }
                catch (TaskCanceledException)
                {
                    if (!cts.Token.IsCancellationRequested)
                        return new FirewallListsAPIResponse { Success = false, Error = "timeout" };

                    throw;
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }
    }
}
