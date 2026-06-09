using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Aikido.Zen.Core.Helpers;
using BenchmarkDotNet.Columns;

namespace Aikido.Zen.Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net10_0, baseline: true, warmupCount: 3, iterationCount: 15, invocationCount: 1)]
    [MinIterationTime(100)]
    [Outliers(Perfolizer.Mathematics.OutlierDetection.OutlierMode.RemoveAll)]
    [HideColumns(Column.StdErr, Column.StdDev, Column.Error, Column.Min, Column.Max, Column.RatioSD)]
    public class RateLimitingHelperBenchmarks
    {
        private const int TargetChecksPerIteration = 1_000_000;

        private string[] _keys;
        private string[] _newKeys;

        [Params(1000, 100_000)] // Different numbers of unique keys
        public int KeyCount { get; set; }

        [Params(60_000)] // Keep the window above benchmark duration so expiry timing does not dominate results.
        public int WindowSizeInMS { get; set; }

        [Params(10, 100)] // Different request limits
        public int MaxRequests { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _keys = new string[KeyCount];
            _newKeys = new string[KeyCount];
            for (int i = 0; i < KeyCount; i++)
            {
                _keys[i] = $"test-key-{i}";
                _newKeys[i] = $"new-key-{i}";
            }
        }

        [Benchmark]
        public void FirstRequests()
        {
            var repetitions = RepetitionsFor(KeyCount);
            for (int repetition = 0; repetition < repetitions; repetition++)
            {
                ResetCache();
                for (int i = 0; i < KeyCount; i++)
                {
                    RateLimitingHelper.IsAllowed(_keys[i], WindowSizeInMS, MaxRequests);
                }
            }
        }

        [Benchmark]
        public void SubsequentRequests()
        {
            var repetitions = RepetitionsFor(KeyCount * 3);
            for (int repetition = 0; repetition < repetitions; repetition++)
            {
                ResetAndSeedRequests();
                for (int i = 0; i < KeyCount; i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        RateLimitingHelper.IsAllowed(_keys[i], WindowSizeInMS, MaxRequests);
                    }
                }
            }
        }

        [Benchmark]
        public void MixedRequests()
        {
            var repetitions = RepetitionsFor(KeyCount * 2);
            for (int repetition = 0; repetition < repetitions; repetition++)
            {
                ResetAndSeedRequests();
                for (int i = 0; i < KeyCount; i++)
                {
                    RateLimitingHelper.IsAllowed(_keys[i], WindowSizeInMS, MaxRequests);
                    RateLimitingHelper.IsAllowed(_newKeys[i], WindowSizeInMS, MaxRequests);
                }
            }
        }

        [Benchmark]
        public void HighLoad()
        {
            var checksPerScenario = KeyCount * (MaxRequests + 2);
            var repetitions = RepetitionsFor(checksPerScenario);
            for (int repetition = 0; repetition < repetitions; repetition++)
            {
                ResetCache();
                for (int i = 0; i < KeyCount; i++)
                {
                    var key = _keys[i % (_keys.Length / 10)];
                    for (int j = 0; j < MaxRequests + 2; j++)
                    {
                        RateLimitingHelper.IsAllowed(key, WindowSizeInMS, MaxRequests);
                    }
                }
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            // Reset to default state
            RateLimitingHelper.ResetCache(10000, 120 * 60 * 1000);
        }

        private void ResetCache()
        {
            RateLimitingHelper.ResetCache(KeyCount * 2, WindowSizeInMS * 2);
        }

        private void ResetAndSeedRequests()
        {
            ResetCache();

            for (int i = 0; i < KeyCount; i++)
            {
                RateLimitingHelper.IsAllowed(_keys[i], WindowSizeInMS, MaxRequests);
            }
        }

        private static int RepetitionsFor(int checksPerScenario)
        {
            return (TargetChecksPerIteration + checksPerScenario - 1) / checksPerScenario;
        }
    }
} 
