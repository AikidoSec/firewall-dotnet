using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Aikido.Zen.Core.Models
{
    /// <summary>
    /// A thread-safe dictionary derived from ConcurrentDictionary with a fixed capacity that evicts the least frequently used item
    /// when the capacity is reached. It tracks usage frequency using the HitCount base class.
    /// Frequency (HitCount) is intended to be incremented explicitly by the calling code upon relevant actions (e.g., updates),
    /// not automatically on read access (TryGetValue or indexer get).
    /// </summary>
    /// <typeparam name="K">The type of the keys in the dictionary.</typeparam>
    /// <typeparam name="V">The type of the values in the dictionary, which must inherit from HitCount.</typeparam>
    public class ConcurrentLFUDictionary<K, V> : ConcurrentDictionary<K, V> where V : HitCount
    {
        private readonly int _maxItems;
        private readonly ReaderWriterLockSlim _evictionLock = new ReaderWriterLockSlim();

        /// <summary>
        /// Gets the current number of items in the dictionary.
        /// </summary>
        public long Size => base.Count;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrentLFUDictionary{K, V}"/> class
        /// with the specified maximum number of items.
        /// </summary>
        /// <param name="maxItems">The maximum number of items the dictionary can hold.</param>
        /// <exception cref="ArgumentException">Thrown if maxItems is less than or equal to zero.</exception>
        public ConcurrentLFUDictionary(int maxItems)
            : base(Environment.ProcessorCount, maxItems)
        {
            if (maxItems <= 0)
            {
                throw new ArgumentException("Maximum items must be greater than zero.", nameof(maxItems));
            }
            _maxItems = maxItems;
        }

        /// <summary>
        /// Tries to get the value associated with the specified key from the dictionary.
        /// Does NOT increment the hit count on access.
        /// </summary>
        /// <param name="key">The key of the item to retrieve.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if found; otherwise, the default value.</param>
        /// <returns>true if the key was found in the dictionary; otherwise, false.</returns>
        public new bool TryGetValue(K key, out V value)
        {
            // Do not increment hits on simple read access
            return base.TryGetValue(key, out value);
        }

        /// <summary>
        /// Adds a key-value pair to the dictionary if the key does not already exist,
        /// or updates the key-value pair if the key already exists.
        /// If adding a new key causes the dictionary to exceed its maximum capacity,
        /// the item with the lowest hit count is removed.
        /// The indexer get accessor does NOT increment hits.
        /// The indexer set accessor replaces the item, effectively resetting its hit count to the new item's initial value.
        /// </summary>
        /// <param name="key">The key of the item to add or update.</param>
        /// <returns>The value associated with the specified key.</returns>
        public new V this[K key]
        {
            get
            {
                // Do not increment hits on simple read access
                if (base.TryGetValue(key, out var value))
                {
                    return value;
                }
                throw new KeyNotFoundException($"The key '{key}' was not found in the dictionary.");
            }
            set => Set(key, value); // Delegate to Set method
        }

        /// <summary>
        /// Adds or updates a key-value pair in the dictionary by replacing the existing value.
        /// If adding a new key causes the dictionary to exceed its maximum capacity,
        /// the item with the lowest hit count is removed.
        /// Note: This method replaces the existing value if the key exists, resetting the hit count.
        /// Use explicit increment calls after updates if needed.
        /// </summary>
        /// <param name="key">The key of the item to add or update.</param>
        /// <param name="value">The value of the item to add or update.</param>
        public void Set(K key, V value)
        {
            bool keyExisted = base.ContainsKey(key);
            base.AddOrUpdate(key, value, (k, existingVal) => value);

            if (!keyExisted && base.Count > _maxItems)
            {
                EvictLeastFrequentlyUsed();
            }
        }

        /// <summary>
        /// Attempts to add the specified key and value to the dictionary.
        /// If adding the key causes the dictionary to exceed its maximum capacity,
        /// the item with the lowest hit count is removed.
        /// </summary>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="value">The value of the element to add. The value can be null for reference types.</param>
        /// <returns>true if the key/value pair was added to the dictionary successfully; otherwise, false.</returns>
        public new bool TryAdd(K key, V value)
        {
            if (base.TryAdd(key, value))
            {
                if (base.Count > _maxItems)
                {
                    EvictLeastFrequentlyUsed();
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Attempts to remove and return the value that has the specified key from the dictionary.
        /// </summary>
        /// <param name="key">The key of the element to remove.</param>
        /// <param name="value">When this method returns, contains the object removed from the dictionary, or the default value if the key does not exist.</param>
        /// <returns>true if the object was removed successfully; otherwise, false.</returns>
        public new bool TryRemove(K key, out V value)
        {
            return base.TryRemove(key, out value);
        }

        /// <summary>
        /// Removes the item with the specified key from the dictionary.
        /// </summary>
        /// <param name="key">The key of the item to remove.</param>
        /// <returns>true if the item was successfully removed; otherwise, false.</returns>
        public bool Delete(K key)
        {
            return base.TryRemove(key, out _);
        }

        /// <summary>
        /// Removes all items from the dictionary.
        /// </summary>
        public void Clear()
        {
            base.Clear();
        }

        /// <summary>
        /// Gets the keys currently present in the dictionary.
        /// </summary>
        /// <returns>An enumerable collection of the keys.</returns>
        public IEnumerable<K> GetKeys()
        {
            return base.Keys;
        }

        /// <summary>
        /// Evicts the item with the lowest hit count.
        /// This method acquires a write lock to ensure thread safety during eviction.
        /// </summary>
        private void EvictLeastFrequentlyUsed()
        {
            _evictionLock.EnterWriteLock();
            try
            {
                if (base.Count <= _maxItems)
                {
                    return;
                }

                var lfuItem = this.OrderBy(pair => pair.Value.Hits).FirstOrDefault();

                if (!lfuItem.Equals(default(KeyValuePair<K, V>)))
                {
                    base.TryRemove(lfuItem.Key, out _);
                }
            }
            finally
            {
                _evictionLock.ExitWriteLock();
            }
        }
    }
}
