using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Jobs;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net10_0, baseline: true)]
    [Outliers(Perfolizer.Mathematics.OutlierDetection.OutlierMode.RemoveAll)]
    [HideColumns(Column.StdErr, Column.StdDev, Column.Error, Column.Min, Column.Max, Column.RatioSD)]
    public class LRUCacheBenchmarks
    {
        private const int TTLInMs = 5000;
        private string[] _keys;
        private string[] _values;

        [Params(1000, 10000)]
        public int CacheSize { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _keys = new string[CacheSize];
            _values = new string[CacheSize];

            for (var i = 0; i < CacheSize; i++)
            {
                _keys[i] = $"key{i}";
                _values[i] = $"value{i}";
            }
        }

        [Benchmark]
        public long Set_NewItems()
        {
            var cache = CreateHalfFullCache();

            for (var i = CacheSize / 2; i < CacheSize; i++)
            {
                cache.Set(_keys[i], _values[i]);
            }

            return cache.Size;
        }

        [Benchmark]
        public int Get_ExistingItems()
        {
            var cache = CreateHalfFullCache();
            var hits = 0;

            for (var i = 0; i < CacheSize / 2; i++)
            {
                if (cache.TryGetValue(_keys[i], out _))
                {
                    hits++;
                }
            }

            return hits;
        }

        [Benchmark]
        public long MixedOperations()
        {
            var cache = CreateHalfFullCache();

            for (var i = 0; i < CacheSize / 4; i++)
            {
                cache.Set(_keys[i], _values[i] + "_updated");
                cache.TryGetValue(_keys[i], out _);
                cache.Set($"new{i}", $"value{i}");
                cache.TryGetValue($"missing{i}", out _);
            }

            return cache.Size;
        }

        private LRUCache<string, string> CreateHalfFullCache()
        {
            var cache = new LRUCache<string, string>(CacheSize, TTLInMs);

            for (var i = 0; i < CacheSize / 2; i++)
            {
                cache.Set(_keys[i], _values[i]);
            }

            return cache;
        }
    }
}
