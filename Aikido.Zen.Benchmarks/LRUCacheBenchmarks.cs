using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Aikido.Zen.Core.Models;
using System;
using System.Collections.Generic;
using BenchmarkDotNet.Columns;

namespace Aikido.Zen.Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net48, baseline: false, warmupCount: 1, iterationCount: 2)]
    [SimpleJob(RuntimeMoniker.Net80, baseline: true, warmupCount: 1, iterationCount: 2)]
    [HideColumns(Column.StdErr, Column.StdDev, Column.Error, Column.Min, Column.Max, Column.RatioSD)]
    public class LRUCacheBenchmarks
    {
        private LRUCache<string, string> _cache;
        private string[] _keys;
        private string[] _values;

        [Params(100, 1000, 10000)] // Test different cache sizes
        public int CacheSize { get; set; }

        [Params(1000, 5000)] // Test different TTLs (0 = no TTL, 1000ms, 5000ms)
        public int TTLInMs { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _cache = new LRUCache<string, string>(CacheSize, TTLInMs);
            _keys = new string[CacheSize];
            _values = new string[CacheSize];

            // Initialize test data
            for (int i = 0; i < CacheSize; i++)
            {
                _keys[i] = $"key{i}";
                _values[i] = $"value{i}";
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
            // Add items to the second half of the cache
            for (int i = CacheSize / 2; i < CacheSize; i++)
            {
                _cache.Set(_keys[i], _values[i]);
            }
        }

        [Benchmark]
        public void Set_ExistingItems()
        {
            // Update existing items in the first half
            for (int i = 0; i < CacheSize / 2; i++)
            {
                _cache.Set(_keys[i], _values[i] + "_updated");
            }
        }

        [Benchmark]
        public void Get_ExistingItems()
        {
            // Get existing items from the first half
            for (int i = 0; i < CacheSize / 2; i++)
            {
                _cache.TryGetValue(_keys[i], out _);
            }
        }

        [Benchmark]
        public void Get_NonExistentItems()
        {
            // Try to get non-existent items
            for (int i = 0; i < CacheSize / 2; i++)
            {
                _cache.TryGetValue($"nonexistent{i}", out _);
            }
        }

        [Benchmark]
        public void MixedOperations()
        {
            // Mix of operations to simulate real-world usage
            for (int i = 0; i < CacheSize / 4; i++)
            {
                _cache.Set(_keys[i], _values[i] + "_new"); // Update
                _cache.TryGetValue(_keys[i], out _); // Get existing
                _cache.Set($"new{i}", $"value{i}"); // Add new
                _cache.TryGetValue($"nonexistent{i}", out _); // Get non-existent
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _cache.Clear();
        }
    }
} 