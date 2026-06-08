using System.Net;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Aikido.Zen.Core.Sinks;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using System;
using System.Threading.Tasks;
using System.Net.Http;
using BenchmarkDotNet.Columns;

namespace Aikido.Zen.Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net10_0, baseline: true, warmupCount: 2, iterationCount: 10)]
    [HideColumns(Column.StdErr, Column.StdDev, Column.Error, Column.Min, Column.Max, Column.RatioSD)]
    public class PatchBenchmarks
    {
        private const string DefaultRequestUri = "http://localhost:5080/health";

        private HttpClient _httpClient;
        private string _requestUri;

        [GlobalSetup(Targets = new[] { nameof(HttpClientUnpatched), nameof(HttpWebRequestUnpatched) })]
        public void UnpatchedSetup()
        {
            Setup();
            Patcher.Unpatch();
        }

        [GlobalSetup(Targets = new[] { nameof(HttpClientPatched), nameof(HttpWebRequestPatched) })]
        public void PatchedSetup()
        {
            Setup();
            Patcher.PatchSinks(() => null);
        }

        private void Setup()
        {
            Cleanup();

            _requestUri = Environment.GetEnvironmentVariable("PATCH_BENCHMARK_URL");
            if (string.IsNullOrWhiteSpace(_requestUri))
            {
                _requestUri = DefaultRequestUri;
            }

            _httpClient = new HttpClient();
            var apiHttpClient = ApiClientHttpClientFactory.Create();
            var reportingAPIClient = new ReportingAPIClient(apiHttpClient);
            var runtimeAPIClient = new RuntimeAPIClient(apiHttpClient);
            Agent.NewInstance(new ZenApi(reportingAPIClient, runtimeAPIClient));
        }

        [Benchmark()]
        public async Task HttpClientUnpatched()
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, _requestUri))
            using (await _httpClient.SendAsync(request))
            {
            }
        }

        [Benchmark]
        public async Task HttpClientPatched()
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, _requestUri))
            using (await _httpClient.SendAsync(request))
            {
            }
        }

        [Benchmark()]
        public void HttpWebRequestUnpatched()
        {
            var webRequest = (HttpWebRequest)WebRequest.Create(_requestUri);
            using (webRequest.GetResponse())
            {
            }
        }

        [Benchmark]
        public void HttpWebRequestPatched()
        {
            var webRequest = (HttpWebRequest)WebRequest.Create(_requestUri);
            using (webRequest.GetResponse())
            {
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _httpClient?.Dispose();
            _httpClient = null;
            _requestUri = null;
        }
    }
}
