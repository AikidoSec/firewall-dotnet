using System.Net;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Aikido.Zen.Core.Sinks;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using BenchmarkDotNet.Columns;

namespace Aikido.Zen.Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net10_0, baseline: true, warmupCount: 1, iterationCount: 2)]
    [HideColumns(Column.StdErr, Column.StdDev, Column.Error, Column.Min, Column.Max, Column.RatioSD)]
    public class PatchBenchmarks
    {
        private static readonly byte[] ResponseBytes = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nOK");

        private HttpClient _httpClient;
        private TcpListener _server;
        private CancellationTokenSource _serverCancellation;
        private Task _serverTask;
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

            StartServer();
            _httpClient = new HttpClient();
            var apiHttpClient = ApiClientHttpClientFactory.Create();
            var reportingAPIClient = new ReportingAPIClient(apiHttpClient);
            var runtimeAPIClient = new RuntimeAPIClient(apiHttpClient);
            Agent.NewInstance(new ZenApi(reportingAPIClient, runtimeAPIClient));
        }

        private void StartServer()
        {
            _serverCancellation = new CancellationTokenSource();
            _server = new TcpListener(IPAddress.Loopback, 0);
            _server.Start();

            var port = ((IPEndPoint)_server.LocalEndpoint).Port;
            _requestUri = $"http://127.0.0.1:{port}/";
            _serverTask = Task.Run(() => RunServerAsync(_serverCancellation.Token));
        }

        private async Task RunServerAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _server.AcceptTcpClientAsync();
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException)
                {
                    break;
                }

                _ = Task.Run(() => RespondAsync(client), cancellationToken);
            }
        }

        private static async Task RespondAsync(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            {
                await stream.WriteAsync(ResponseBytes, 0, ResponseBytes.Length);
            }
        }

        [Benchmark()]
        public async Task HttpClientUnpatched()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, _requestUri);
            using (await _httpClient.SendAsync(request))
            {
            }
        }

        [Benchmark]
        public async Task HttpClientPatched()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, _requestUri);
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

            _serverCancellation?.Cancel();
            _server?.Stop();

            try
            {
                _serverTask?.Wait(TimeSpan.FromSeconds(1));
            }
            catch (AggregateException)
            {
            }

            _serverCancellation?.Dispose();
            _serverCancellation = null;
            _server = null;
            _serverTask = null;
            _requestUri = null;
        }
    }
}
