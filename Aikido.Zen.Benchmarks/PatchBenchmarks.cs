using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Jobs;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Sinks;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Aikido.Zen.Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net10_0, baseline: true)]
    [Outliers(Perfolizer.Mathematics.OutlierDetection.OutlierMode.RemoveAll)]
    [HideColumns(Column.StdErr, Column.StdDev, Column.Error, Column.Min, Column.Max, Column.RatioSD)]
    public class PatchBenchmarks
    {
        private Agent _agent;
        private Context _context;
        private HttpClient _httpClient;

        [GlobalSetup(Target = nameof(HttpClientSendAsyncUnpatched))]
        public void UnpatchedSetup()
        {
            SetupAgentAndHttpClient();
            Patcher.Unpatch();
        }

        [GlobalSetup(Target = nameof(HttpClientSendAsyncPatched))]
        public void PatchedSetup()
        {
            SetupAgentAndHttpClient();
            Patcher.Unpatch();
            Patcher.PatchSinks(() => _context);
        }

        [Benchmark]
        public async Task<HttpStatusCode> HttpClientSendAsyncUnpatched()
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, "/resource"))
            using (var response = await _httpClient.SendAsync(request).ConfigureAwait(false))
            {
                return response.StatusCode;
            }
        }

        [Benchmark]
        public async Task<HttpStatusCode> HttpClientSendAsyncPatched()
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, "/resource"))
            using (var response = await _httpClient.SendAsync(request).ConfigureAwait(false))
            {
                return response.StatusCode;
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _httpClient?.Dispose();
            Patcher.Unpatch();
            _agent?.Dispose();
        }

        private void SetupAgentAndHttpClient()
        {
            _context = new Context
            {
                Method = "GET",
                Route = "/outbound",
                Url = "https://app.local/outbound",
                RemoteAddress = "203.0.113.10"
            };

            _agent = Agent.NewInstance(new BenchmarkZenApi());
            _agent.ClearContext();

            _httpClient = new HttpClient(new BenchmarkHttpMessageHandler())
            {
                BaseAddress = new Uri("https://benchmark.example")
            };
        }

        private sealed class BenchmarkHttpMessageHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }
        }

        private sealed class BenchmarkZenApi : IZenApi
        {
            public IReportingAPIClient Reporting { get; } = new BenchmarkReportingAPIClient();

            public IRuntimeAPIClient Runtime { get; } = new BenchmarkRuntimeAPIClient();
        }

        private sealed class BenchmarkReportingAPIClient : IReportingAPIClient
        {
            public Task<ReportingAPIResponse> ReportAsync(string token, object @event, CancellationToken cancellationToken)
            {
                return Task.FromResult(new ReportingAPIResponse { Success = true });
            }

            public Task<FirewallListsAPIResponse> GetFirewallLists(string token, CancellationToken cancellationToken)
            {
                return Task.FromResult(new FirewallListsAPIResponse { Success = true });
            }
        }

        private sealed class BenchmarkRuntimeAPIClient : IRuntimeAPIClient
        {
            public Task<ConfigLastUpdatedAPIResponse> GetConfigLastUpdated(string token, CancellationToken cancellationToken)
            {
                return Task.FromResult(new ConfigLastUpdatedAPIResponse { Success = true });
            }

            public Task<ReportingAPIResponse> GetConfig(string token, CancellationToken cancellationToken)
            {
                return Task.FromResult(new ReportingAPIResponse { Success = true });
            }
        }
    }
}
