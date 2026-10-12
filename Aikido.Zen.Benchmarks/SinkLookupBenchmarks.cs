using Aikido.Zen.Core;
using Aikido.Zen.DotNetCore;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Jobs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ZenApi = Aikido.Zen.DotNetCore.Zen;

namespace Aikido.Zen.Benchmarks
{
    /// <summary>
    /// Lookups the patched sinks perform on every inspected call, such as an outbound request, file access or SQL query.
    /// </summary>
    [SimpleJob(RuntimeMoniker.Net10_0, baseline: true)]
    [MemoryDiagnoser]
    [Outliers(Perfolizer.Mathematics.OutlierDetection.OutlierMode.RemoveAll)]
    [HideColumns(Column.StdErr, Column.StdDev, Column.Error, Column.Min, Column.Max, Column.RatioSD)]
    public class SinkLookupBenchmarks
    {
        private ServiceProvider _serviceProvider;

        [GlobalSetup]
        public void Setup()
        {
            // Register Zen the way an ASP.NET Core application does, inside a request
            _serviceProvider = new ServiceCollection()
                .AddZenFirewall()
                .BuildServiceProvider();
            var httpContextAccessor = _serviceProvider.GetRequiredService<IHttpContextAccessor>();
            httpContextAccessor.HttpContext = new DefaultHttpContext();
            httpContextAccessor.HttpContext.Items["Aikido.Zen.Context"] = new Context();
            ZenApi.Initialize(_serviceProvider, httpContextAccessor);

            _ = Agent.Instance;
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            Agent.Instance.Dispose();
            ZenApi.Initialize(null, null);
            _serviceProvider.Dispose();
        }

        [Benchmark]
        public Context GetContext()
        {
            return ZenApi.GetContext();
        }

        [Benchmark]
        public Agent AgentInstance()
        {
            return Agent.Instance;
        }
    }
}
