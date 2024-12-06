using System.Net;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Aikido.Zen.Core.Patches;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using System;
using System.Threading.Tasks;
using System.Net.Http;

namespace Aikido.Zen.Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net48, baseline: false, warmupCount: 1, iterationCount: 5)]
    [SimpleJob(RuntimeMoniker.Net80, baseline: true, warmupCount: 1, iterationCount: 5)]
    [SimpleJob(RuntimeMoniker.NativeAot80, baseline: false, warmupCount: 1, iterationCount: 5)]
    public class PatchBenchmarks
    {
        private HttpClient _httpClient;
        private HttpWebRequest _webRequest;
        private static bool _patchesApplied;

        [GlobalSetup(Targets = [nameof(HttpClientUnpatched), nameof(HttpWebRequestUnpatched)])]
        public void UnpatchedSetup()
        {
            _httpClient = new HttpClient();
            Agent.GetInstance(new ZenApi(new ReportingAPIClient(new Uri("https://guard.aikido.dev"))));

            Patcher.Unpatch();
        }

        [GlobalSetup(Targets = [nameof(HttpClientPatched), nameof(HttpWebRequestPatched)])]
        public void PatchedSetup()
        {
            _httpClient = new HttpClient();
            Agent.GetInstance(new ZenApi(new ReportingAPIClient(new Uri("https://guard.aikido.dev"))));

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
