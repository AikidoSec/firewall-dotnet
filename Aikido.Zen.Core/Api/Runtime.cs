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
        private readonly HttpClient _httpClient;

        public RuntimeAPIClient()
        {
            var handler = new HttpClientHandler();
            handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            _httpClient = new HttpClient(handler);
        }

        public async Task<CheckConfigAPIResponse> GetConfigVersion(string token)
        {
            using (var cts = new CancellationTokenSource(5000))
            {
                var request = APIHelper.CreateRequest(token, new Uri(EnvironmentHelper.AikidoRealtimeUrl), "config", HttpMethod.Get);

                try
                {
                    var response = await _httpClient.SendAsync(request, cts.Token);
                    return APIHelper.ToAPIResponse<CheckConfigAPIResponse>(response);
                }
                catch (TaskCanceledException)
                {
                    if (!cts.Token.IsCancellationRequested)
                        return new CheckConfigAPIResponse { Success = false, Error = "timeout" };

                    throw;
                }
                catch (Exception ex)
                {
                    throw new Exception("An error occurred while reporting", ex);
                }
            }
        }

        public async Task<CheckConfigAPIResponse> GetConfig(string token)
        {
            using (var cts = new CancellationTokenSource(5000))
            {
                var request = APIHelper.CreateRequest(token, new Uri(EnvironmentHelper.AikidoUrl), "/api/runtime/config", HttpMethod.Get);

                try
                {
                    var response = await _httpClient.SendAsync(request, cts.Token);
                    return APIHelper.ToAPIResponse<CheckConfigAPIResponse>(response);
                }
                catch (TaskCanceledException)
                {
                    if (!cts.Token.IsCancellationRequested)
                        return new CheckConfigAPIResponse { Success = false, Error = "timeout" };

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
