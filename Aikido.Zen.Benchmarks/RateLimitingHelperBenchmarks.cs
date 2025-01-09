using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Aikido.Zen.Core.Helpers;
using BenchmarkDotNet.Columns;

namespace Aikido.Zen.Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net48, baseline: false, warmupCount: 1, iterationCount: 1, invocationCount: 2)]
    [SimpleJob(RuntimeMoniker.Net80, baseline: true, warmupCount: 1, iterationCount: 1, invocationCount: 2)]
    [HideColumns(Column.StdErr, Column.StdDev, Column.Error, Column.Min, Column.Max, Column.RatioSD)]
    public class RateLimitingHelperBenchmarks
    {
        private string[] _keys;

        [Params(1000, 100_000)] // Different numbers of unique keys
        public int KeyCount { get; set; }

        [Params(1000, 5000)] // Different window sizes in milliseconds
        public int WindowSizeInMS { get; set; }

        [Params(10, 100)] // Different request limits
        public int MaxRequests { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            // Reset the cache with test parameters
            RateLimitingHelper.ResetCache(KeyCount * 2, WindowSizeInMS * 2);
            
            _keys = new string[KeyCount];
            for (int i = 0; i < KeyCount; i++)
            {
                _keys[i] = $"test-key-{i}";
            }
        }

        [Benchmark]
        public void FirstRequests()
        {
            // Simulate first requests for all keys (should all be allowed)
            for (int i = 0; i < KeyCount; i++)
            {
                RateLimitingHelper.IsAllowed(_keys[i], WindowSizeInMS, MaxRequests);
            }
        }

        [Benchmark]
        public void SubsequentRequests()
        {
            // Simulate subsequent requests for existing keys
            for (int i = 0; i < KeyCount; i++)
            {
                for (int j = 0; j < 3; j++) // Make multiple requests per key
                {
                    RateLimitingHelper.IsAllowed(_keys[i], WindowSizeInMS, MaxRequests);
                }
            }
        }

        [Benchmark]
        public void MixedRequests()
        {
            // Mix of new and existing keys
            for (int i = 0; i < KeyCount; i++)
            {
                // Existing key
                RateLimitingHelper.IsAllowed(_keys[i], WindowSizeInMS, MaxRequests);
                
                // New key
                RateLimitingHelper.IsAllowed($"new-key-{i}", WindowSizeInMS, MaxRequests);
            }
        }

        [Benchmark]
        public void HighLoad()
        {
            // Simulate high load with repeated requests to same keys
            for (int i = 0; i < KeyCount; i++)
            {
                string key = _keys[i % (_keys.Length / 10)]; // Reuse keys more frequently
                for (int j = 0; j < MaxRequests + 2; j++) // Intentionally exceed limit
                {
                    RateLimitingHelper.IsAllowed(key, WindowSizeInMS, MaxRequests);
                }
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            // Reset to default state
            RateLimitingHelper.ResetCache(10000, 120 * 60 * 1000);
        }
    }
} 
