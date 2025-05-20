using System;
using System.Collections; // Added for IEnumerable
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Aikido.Zen.Core.Models
{
    /// <summary>
    /// A thread-safe dictionary with a fixed capacity that evicts the least frequently used (LFU) item
    /// when the capacity is reached. It wraps a <see cref="ConcurrentDictionary{K, V}"/> and tracks usage
    /// frequency using the <see cref="HitCount"/> base class for values.
    /// Frequency (HitCount) is intended to be incremented explicitly via the <see cref="AddOrUpdate"/> method,
    /// not automatically on read access (<see cref="TryGet"/>).
    /// </summary>
    /// <typeparam name="K">The type of the keys in the dictionary.</typeparam>
    /// <typeparam name="V">The type of the values in the dictionary, which must inherit from <see cref="HitCount"/>.</typeparam>
    public class ConcurrentLFUDictionary<K, V> : IEnumerable<KeyValuePair<K, V>> where V : HitCount // Implemented IEnumerable
    {
        private readonly ConcurrentDictionary<K, V> _dictionary;
        private readonly int _maxItems;
        private readonly ReaderWriterLockSlim _evictionLock = new ReaderWriterLockSlim();

        /// <summary>
        /// Gets the current number of items in the dictionary.
        /// </summary>
        public int Size => _dictionary.Count; // Use Count for ConcurrentDictionary

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrentLFUDictionary{K, V}"/> class
        /// with the specified maximum number of items.
        /// </summary>
        /// <param name="maxItems">The maximum number of items the dictionary can hold.</param>
        /// <exception cref="ArgumentException">Thrown if maxItems is less than or equal to zero.</exception>
        public ConcurrentLFUDictionary(int maxItems)
        {
            if (maxItems <= 0)
            {
                throw new ArgumentException("Maximum items must be greater than zero.", nameof(maxItems));
            }
            _maxItems = maxItems;
            // Initialize the internal dictionary with appropriate concurrency level and capacity
            _dictionary = new ConcurrentDictionary<K, V>(Environment.ProcessorCount * 2, maxItems);
        }

        /// <summary>
        /// Tries to get the value associated with the specified key from the dictionary.
        /// Does NOT increment the hit count on access.
        /// </summary>
        /// <param name="key">The key of the item to retrieve.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if found; otherwise, the default value.</param>
        /// <returns>true if the key was found in the dictionary; otherwise, false.</returns>
        public bool TryGet(K key, out V value)
        {
            // Does not increment hits on simple read access
            return _dictionary.TryGetValue(key, out value);
        }

        /// <summary>
        /// Adds a key-value pair to the dictionary or updates the value if the key already exists.
        /// Increments the hit count of the added or updated value.
        /// If adding a new key causes the dictionary to exceed its maximum capacity,
        /// the item with the lowest hit count is removed.
        /// </summary>
        /// <param name="key">The key of the item to add or update.</param>
        /// <param name="value">The value to associate with the key.</param>
        /// <returns>The value added or updated.</returns>
        public V AddOrUpdate(K key, V value)
        {
            _evictionLock.EnterWriteLock();
            try
            {
                bool keyExisted = _dictionary.ContainsKey(key);

                // Evict *before* adding if the key is new and dictionary is full
                // otherwise or lfu candidate is always our item we are adding
                if (!keyExisted && Size >= _maxItems) // Check >= because we are *about* to add
                {
                    EvictLeastFrequentlyUsed(); // Assumes lock is held
                }

                _dictionary[key] = value;

                // Increment the hits of the value now associated with the key.
                value.Increment();

                return value;
            }
            finally
            {
                _evictionLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Removes the item with the specified key from the dictionary.
        /// </summary>
        /// <param name="key">The key of the item to remove.</param>
        /// <returns>true if the item was successfully removed; otherwise, false.</returns>
        public bool Delete(K key)
        {
            // ConcurrentDictionary.TryRemove is thread-safe for single operations.
            return _dictionary.TryRemove(key, out _);
        }

        /// <summary>
        /// Removes all items from the dictionary.
        /// </summary>
        public void Clear()
        {
            // ConcurrentDictionary.Clear is thread-safe.
            _dictionary.Clear();
        }

        /// <summary>
        /// Gets the keys currently present in the dictionary.
        /// </summary>
        /// <returns>An enumerable collection of the keys.</returns>
        public IEnumerable<K> GetKeys()
        {
            return _dictionary.Keys;
        }

        /// <summary>
        /// Gets the values currently present in the dictionary.
        /// </summary>
        /// <returns>An enumerable collection of the values.</returns>
        public IEnumerable<V> GetValues()
        {
            return _dictionary.Values;
        }

        /// <summary>
        /// Evicts the item with the lowest hit count.
        /// This method assumes the caller already holds the write lock.
        /// Uses a more efficient algorithm to find the least frequently used item.
        /// </summary>
        private void EvictLeastFrequentlyUsed()
        {

            K keyToRemove = default;
            int minHits = int.MaxValue;
            V valueWithMinHits = default; // Keep track of the value to handle potential ties deterministically (optional)

            // Find the item with minimum hits in a single pass - O(n)
            // Note: Enumerating ConcurrentDictionary is weakly consistent.
            // However, since we hold the write lock during AddOrUpdate which calls this,
            // the state relevant to eviction decision should be stable enough.
            foreach (var pair in _dictionary)
            {
                if (pair.Value.Hits < minHits)
                {
                    minHits = pair.Value.Hits;
                    keyToRemove = pair.Key;
                    valueWithMinHits = pair.Value; // Store the value as well
                }
                // Optional: Add tie-breaking logic here if needed, e.g., based on oldest entry (requires more state)
                // or simply stick with the first one found with minHits.
            }

            // Only attempt removal if a valid key was found
            // Use EqualityComparer<K>.Default to handle default(K) correctly for value types and reference types.
            if (!EqualityComparer<K>.Default.Equals(keyToRemove, default(K)) || _dictionary.ContainsKey(default(K))) // Handle case where default(K) is a valid key
            {
                // Ensure the item we selected based on the snapshot is still the one to remove,
                // or at least that the key still exists. Re-check might be overly cautious given the lock.
                _dictionary.TryRemove(keyToRemove, out _);
            }
            // No finally block needed as we didn't acquire the lock here (caller holds it)
        }


        /// <summary>
        /// Returns an enumerator that iterates through the key-value pairs in the dictionary.
        /// </summary>
        /// <returns>An enumerator for the <see cref="ConcurrentLFUDictionary{K,V}"/>.</returns>
        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            return _dictionary.GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>An <see cref="IEnumerator"/> object that can be used to iterate through the collection.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
