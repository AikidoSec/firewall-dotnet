using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using Aikido.Zen.Core.Helpers;


namespace Aikido.Zen.Core.Api
{
    public class RuntimeAPIClient : IRuntimeAPIClient
    {
        private readonly Uri _runtimeUrl;
        private readonly Uri _aikidoUrl;
        private readonly HttpClient _httpClient;

        public RuntimeAPIClient(Uri runtimeUrl, Uri aikidoUrl)
        {
            var handler = new HttpClientHandler();
            handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            _httpClient = new HttpClient(handler);
            _runtimeUrl = runtimeUrl;
            _aikidoUrl = aikidoUrl;
        }

        public async Task<ReportingAPIResponse> GetConfigVersion(string token)
        {
            using (var cts = new CancellationTokenSource(5000))
            {
                var request = APIHelper.CreateRequest(token, _runtimeUrl, "config", HttpMethod.Get);

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

        public async Task<ReportingAPIResponse> GetConfig(string token)
        {
            using (var cts = new CancellationTokenSource(5000))
            {
                var request = APIHelper.CreateRequest(token, _aikidoUrl, "/api/runtime/config", HttpMethod.Get);

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
