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
        private LRUCache<string, string> _cache;
        private int _nextInsertionKeyIndex;
        private string[] _cachedKeys;
        private string[] _initialValues;
        private string[] _insertionKeys;
        private string[] _missingLookupKeys;
        private string[] _replacementValues;

        [Params(1000, 10000)]
        public int CacheSize { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _cache = new LRUCache<string, string>(CacheSize, TTLInMs);
            _cachedKeys = new string[CacheSize];
            _initialValues = new string[CacheSize];
            _insertionKeys = new string[CacheSize * 2];
            _missingLookupKeys = new string[CacheSize];
            _replacementValues = new string[CacheSize];

            for (var i = 0; i < CacheSize; i++)
            {
                _cachedKeys[i] = $"key{i}";
                _initialValues[i] = $"value{i}";
                _missingLookupKeys[i] = $"missing{i}";
                _replacementValues[i] = $"replacement{i}";
                _cache.Set(_cachedKeys[i], _initialValues[i]);
            }

            for (var i = 0; i < _insertionKeys.Length; i++)
            {
                _insertionKeys[i] = $"inserted{i}";
            }
        }

        [Benchmark]
        public long Set_NewItemsWithEviction()
        {
            for (var i = 0; i < CacheSize; i++)
            {
                _cache.Set(_insertionKeys[_nextInsertionKeyIndex], _initialValues[i]);
                _nextInsertionKeyIndex++;

                if (_nextInsertionKeyIndex == _insertionKeys.Length)
                {
                    _nextInsertionKeyIndex = 0;
                }
            }

            return _cache.Size;
        }

        [Benchmark]
        public long Set_ExistingItems()
        {
            for (var i = 0; i < CacheSize; i++)
            {
                _cache.Set(_cachedKeys[i], _replacementValues[i]);
            }

            return _cache.Size;
        }

        [Benchmark]
        public int Get_ExistingItems()
        {
            var hits = 0;

            for (var i = 0; i < CacheSize; i++)
            {
                if (_cache.TryGetValue(_cachedKeys[i], out _))
                {
                    hits++;
                }
            }

            return hits;
        }

        [Benchmark]
        public int Get_MissingItems()
        {
            var misses = 0;

            for (var i = 0; i < CacheSize; i++)
            {
                if (!_cache.TryGetValue(_missingLookupKeys[i], out _))
                {
                    misses++;
                }
            }

            return misses;
        }

        [Benchmark]
        public long MixedOperations()
        {
            for (var i = 0; i < CacheSize / 4; i++)
            {
                _cache.Set(_cachedKeys[i], _replacementValues[i]);
                _cache.TryGetValue(_cachedKeys[i], out _);
                _cache.Set(_insertionKeys[_nextInsertionKeyIndex], _initialValues[i]);
                _cache.TryGetValue(_missingLookupKeys[i], out _);
                _nextInsertionKeyIndex++;

                if (_nextInsertionKeyIndex == _insertionKeys.Length)
                {
                    _nextInsertionKeyIndex = 0;
                }
            }

            return _cache.Size;
        }
    }
}
