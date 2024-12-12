using System.Net;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Aikido.Zen.Core.Patches;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using System;
using System.Threading.Tasks;
using System.Net.Http;
using BenchmarkDotNet.Columns;

namespace Aikido.Zen.Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net48, baseline: false, warmupCount: 1, iterationCount: 5)]
    [SimpleJob(RuntimeMoniker.Net80, baseline: true, warmupCount: 1, iterationCount: 5)]
    [HideColumns(Column.StdErr, Column.StdDev, Column.Error, Column.Min, Column.Max, Column.RatioSD)]
    public class PatchBenchmarks
    {
        private HttpClient _httpClient;
        private HttpWebRequest _webRequest;
        private static string aikidoUrl = Environment.GetEnvironmentVariable("AIKIDO_URL") ?? "https://guard.aikido.dev";
        private static string aikidoRuntimeUrl = Environment.GetEnvironmentVariable("AIKIDO_REALTIME_URL") ?? "https://runtime.aikido.dev";

        [GlobalSetup(Targets = new[] { nameof(HttpClientUnpatched), nameof(HttpWebRequestUnpatched) })]
        public void UnpatchedSetup()
        {
            _httpClient = new HttpClient();
            var reportingAPIClient = new ReportingAPIClient(new Uri(aikidoUrl));
            var runtimeAPIClient = new RuntimeAPIClient(new Uri(aikidoRuntimeUrl));
            Agent.GetInstance(new ZenApi(reportingAPIClient, runtimeAPIClient));

            Patcher.Unpatch();
        }

        [GlobalSetup(Targets = new[] { nameof(HttpClientPatched), nameof(HttpWebRequestPatched) })]
        public void PatchedSetup()
        {
            _httpClient = new HttpClient();
            var reportingAPIClient = new ReportingAPIClient(new Uri(aikidoUrl));
            var runtimeAPIClient = new RuntimeAPIClient(new Uri(aikidoRuntimeUrl));
            Agent.GetInstance(new ZenApi(reportingAPIClient, runtimeAPIClient));

            Patcher.Patch();
        }

        [Benchmark()]
        public async Task HttpClientUnpatched()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "http://example.com");
            await _httpClient.SendAsync(request);
        }

        [Benchmark]
        public async Task HttpClientPatched()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "http://example.com");
            await _httpClient.SendAsync(request);
        }

        [Benchmark()]
        public void HttpWebRequestUnpatched()
        {
            _webRequest = (HttpWebRequest)WebRequest.Create("http://example.com");
            _webRequest.GetResponse();
        }

        [Benchmark]
        public void HttpWebRequestPatched()
        {
            _webRequest = (HttpWebRequest)WebRequest.Create("http://example.com");
            _webRequest.GetResponse();
        }
    }
}
