using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Jobs;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net10_0, baseline: true)]
    [Outliers(Perfolizer.Mathematics.OutlierDetection.OutlierMode.RemoveAll)]
    [HideColumns(Column.StdErr, Column.StdDev, Column.Error, Column.Min, Column.Max, Column.RatioSD)]
    public class RateLimitingHelperBenchmarks
    {
        private const int WindowSizeInMS = 1000;
        private const int MaxRequests = 10;
        private string[] _keys;

        [Params(1000, 10000)]
        public int KeyCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _keys = new string[KeyCount];
            for (var i = 0; i < KeyCount; i++)
            {
                _keys[i] = $"test-key-{i}";
            }
        }

        [Benchmark]
        public int FirstRequests()
        {
            ResetCache();

            var allowed = 0;
            for (var i = 0; i < KeyCount; i++)
            {
                if (RateLimitingHelper.IsAllowed(_keys[i], WindowSizeInMS, MaxRequests))
                {
                    allowed++;
                }
            }

            return allowed;
        }

        [Benchmark]
        public int SubsequentRequests()
        {
            ResetCache();
            SeedRequests(1);

            var allowed = 0;
            for (var i = 0; i < KeyCount; i++)
            {
                if (RateLimitingHelper.IsAllowed(_keys[i], WindowSizeInMS, MaxRequests))
                {
                    allowed++;
                }
            }

            return allowed;
        }

        [Benchmark]
        public int RequestsOverLimit()
        {
            ResetCache();
            SeedRequests(MaxRequests);

            var blocked = 0;
            for (var i = 0; i < KeyCount; i++)
            {
                if (!RateLimitingHelper.IsAllowed(_keys[i], WindowSizeInMS, MaxRequests))
                {
                    blocked++;
                }
            }

            return blocked;
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            RateLimitingHelper.ResetCache(10000, 120 * 60 * 1000);
        }

        private void SeedRequests(int requestsPerKey)
        {
            for (var i = 0; i < KeyCount; i++)
            {
                for (var request = 0; request < requestsPerKey; request++)
                {
                    RateLimitingHelper.IsAllowed(_keys[i], WindowSizeInMS, MaxRequests);
                }
            }
        }

        private void ResetCache()
        {
            RateLimitingHelper.ResetCache(KeyCount * 2, WindowSizeInMS * 2);
        }
    }
}
