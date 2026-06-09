using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Aikido.Zen.Core.Models;
using System;
using System.Collections.Generic;
using BenchmarkDotNet.Columns;

namespace Aikido.Zen.Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net10_0, baseline: true, warmupCount: 3, iterationCount: 15, invocationCount: 1)]
    [MinIterationTime(100)]
    [Outliers(Perfolizer.Mathematics.OutlierDetection.OutlierMode.RemoveAll)]
    [HideColumns(Column.StdErr, Column.StdDev, Column.Error, Column.Min, Column.Max, Column.RatioSD)]
    public class LRUCacheBenchmarks
    {
        private LRUCache<string, string> _cache;
        private string[] _keys;
        private string[] _values;
        private string[] _updatedValues;
        private string[] _newKeys;
        private string[] _missingKeys;

        [Params(100, 1000, 10000)] // Test different cache sizes
        public int CacheSize { get; set; }

        [Params(60_000)] // Keep expiry outside the benchmark duration so TTL timing does not dominate results.
        public int TTLInMs { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _cache = new LRUCache<string, string>(CacheSize, TTLInMs);
            _keys = new string[CacheSize];
            _values = new string[CacheSize];
            _updatedValues = new string[CacheSize];
            _newKeys = new string[CacheSize];
            _missingKeys = new string[CacheSize];

            // Initialize test data
            for (int i = 0; i < CacheSize; i++)
            {
                _keys[i] = $"key{i}";
                _values[i] = $"value{i}";
                _updatedValues[i] = $"value{i}_updated";
                _newKeys[i] = $"new{i}";
                _missingKeys[i] = $"nonexistent{i}";
            }

            // Pre-fill cache to half capacity
            for (int i = 0; i < CacheSize / 2; i++)
            {
                _cache.Set(_keys[i], _values[i]);
            }
        }

        [Benchmark]
        public void Set_NewItems()
        {
            for (int repetition = 0; repetition < RepetitionsForCacheSize(); repetition++)
            {
                // Add items to the second half of the cache
                for (int i = CacheSize / 2; i < CacheSize; i++)
                {
                    _cache.Set(_keys[i], _values[i]);
                }
            }
        }

        [Benchmark]
        public void Set_ExistingItems()
        {
            for (int repetition = 0; repetition < RepetitionsForCacheSize(); repetition++)
            {
                // Update existing items in the first half
                for (int i = 0; i < CacheSize / 2; i++)
                {
                    _cache.Set(_keys[i], _updatedValues[i]);
                }
            }
        }

        [Benchmark]
        public void Get_ExistingItems()
        {
            for (int repetition = 0; repetition < RepetitionsForCacheSize(); repetition++)
            {
                // Get existing items from the first half
                for (int i = 0; i < CacheSize / 2; i++)
                {
                    _cache.TryGetValue(_keys[i], out _);
                }
            }
        }

        [Benchmark]
        public void Get_NonExistentItems()
        {
            for (int repetition = 0; repetition < RepetitionsForCacheSize(); repetition++)
            {
                // Try to get non-existent items
                for (int i = 0; i < CacheSize / 2; i++)
                {
                    _cache.TryGetValue(_missingKeys[i], out _);
                }
            }
        }

        [Benchmark]
        public void MixedOperations()
        {
            for (int repetition = 0; repetition < RepetitionsForCacheSize(); repetition++)
            {
                // Mix of operations to simulate real-world usage
                for (int i = 0; i < CacheSize / 4; i++)
                {
                    _cache.Set(_keys[i], _updatedValues[i]); // Update
                    _cache.TryGetValue(_keys[i], out _); // Get existing
                    _cache.Set(_newKeys[i], _values[i]); // Add new
                    _cache.TryGetValue(_missingKeys[i], out _); // Get non-existent
                }
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _cache.Clear();
        }

        private int RepetitionsForCacheSize()
        {
            return Math.Max(1, 5_000_000 / CacheSize);
        }
    }
}
