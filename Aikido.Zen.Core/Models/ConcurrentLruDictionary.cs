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
    /// </summary>
    /// <typeparam name="K">The type of the keys in the dictionary.</typeparam>
    /// <typeparam name="V">The type of the values in the dictionary, which must inherit from HitCount.</typeparam>
    public class ConcurrentLruDictionary<K, V> : ConcurrentDictionary<K, V> where V : HitCount
    {
        private readonly int _maxItems;
        private readonly ReaderWriterLockSlim _evictionLock = new ReaderWriterLockSlim(); // To protect eviction logic

        /// <summary>
        /// Gets the current number of items in the dictionary.
        /// </summary>
        public long Size => base.Count;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrentLruDictionary{K, V}"/> class
        /// with the specified maximum number of items.
        /// </summary>
        /// <param name="maxItems">The maximum number of items the dictionary can hold.</param>
        /// <exception cref="ArgumentException">Thrown if maxItems is less than or equal to zero.</exception>
        public ConcurrentLruDictionary(int maxItems)
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
        /// If the key is found, its hit count is incremented.
        /// Overrides the base TryGetValue to add hit counting.
        /// </summary>
        /// <param name="key">The key of the item to retrieve.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if found; otherwise, the default value.</param>
        /// <returns>true if the key was found in the dictionary; otherwise, false.</returns>
        public new bool TryGetValue(K key, out V value)
        {
            if (base.TryGetValue(key, out value))
            {
                value?.Increment(); // Increment hit count on access
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>
        /// Adds a key-value pair to the dictionary if the key does not already exist,
        /// or updates the key-value pair if the key already exists.
        /// If adding a new key causes the dictionary to exceed its maximum capacity,
        /// the item with the lowest hit count is removed.
        /// This method hides the base indexer to incorporate eviction logic.
        /// </summary>
        /// <param name="key">The key of the item to add or update.</param>
        /// <returns>The value associated with the specified key.</returns>
        public new V this[K key]
        {
            get => base[key];
            set => Set(key, value);
        }

        /// <summary>
        /// Adds or updates a key-value pair in the dictionary.
        /// If adding a new key causes the dictionary to exceed its maximum capacity,
        /// the item with the lowest hit count is removed.
        /// </summary>
        /// <param name="key">The key of the item to add or update.</param>
        /// <param name="value">The value of the item to add or update.</param>
        public void Set(K key, V value)
        {
            bool addedNew = false;
            base.AddOrUpdate(key, value, (k, existingVal) => value);

            // Check if it was an add operation that might require eviction
            // This is less direct than TryAdd, but necessary when using AddOrUpdate or indexer
            // We assume if the value being set is the one passed in, it might be new or an update
            // A better check might be needed if value equality is complex or if AddOrUpdate's behavior is critical
            if (!base.ContainsKey(key) || base.Count > _maxItems) // Simplified check
            {
                // Check if eviction is needed *after* the add/update
                if (base.Count > _maxItems)
                {
                    EvictLeastFrequentlyUsed();
                }
            }
        }

        /// <summary>
        /// Attempts to add the specified key and value to the dictionary.
        /// If adding the key causes the dictionary to exceed its maximum capacity,
        /// the item with the lowest hit count is removed.
        /// Hides the base TryAdd method.
        /// </summary>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="value">The value of the element to add. The value can be null for reference types.</param>
        /// <returns>true if the key/value pair was added to the dictionary successfully; otherwise, false.</returns>
        public new bool TryAdd(K key, V value)
        {
            if (base.TryAdd(key, value))
            {
                // Added a new item, check if eviction is needed
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
        /// Hides the base TryRemove method.
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
                // Double-check count within the lock
                if (base.Count <= _maxItems)
                {
                    return;
                }

                // Find the item with the minimum hits
                // Note: Iterating ConcurrentDictionary can be snapshot-based.
                // Ordering the entire dictionary might be inefficient.
                var lfuItem = this.OrderBy(pair => pair.Value.Hits).FirstOrDefault();

                if (!lfuItem.Equals(default(KeyValuePair<K, V>))) // Check if an item was found
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
