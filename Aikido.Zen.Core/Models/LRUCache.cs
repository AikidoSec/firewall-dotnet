using System;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;

namespace Aikido.Zen.Core.Models
{
    /// <summary>
    /// A thread-safe Least Recently Used (LRU) Cache with a fixed capacity and TTL.
    /// </summary>
    /// <typeparam name="K">The type of the keys in the cache.</typeparam>
    /// <typeparam name="V">The type of the values in the cache.</typeparam>
    public class LRUCache<K, V>
    {
        private readonly int maxCapacity;
        private readonly int ttlInMs;
        private readonly Dictionary<K, LinkedListNode<CacheItem>> cacheMap;
        private readonly LinkedList<CacheItem> lruList;
        private readonly ReaderWriterLockSlim cacheLock = new ReaderWriterLockSlim();

        public long Size => cacheMap.Count;

        /// <summary>
        /// Initializes a new instance of the <see cref="LRUCache{K, V}"/> class with the specified capacity and TTL.
        /// </summary>
        /// <param name="capacity">The maximum number of items that can be held in the cache.</param>
        /// <param name="ttlInMs">The time-to-live in milliseconds for cache items. Use 0 for no expiration.</param>
        public LRUCache(int capacity, int ttlInMs = 0)
        {
            if (capacity <= 0)
            {
                throw new ArgumentException("Capacity must be greater than zero.");
            }

            if (ttlInMs < 0)
            {
                throw new ArgumentException("TTL must be greater than or equal to zero.");
            }

            maxCapacity = capacity;
            this.ttlInMs = ttlInMs;
            cacheMap = new Dictionary<K, LinkedListNode<CacheItem>>(capacity);
            lruList = new LinkedList<CacheItem>();
        }

        /// <summary>
        /// Tries to get the value associated with the specified key from the cache.
        /// </summary>
        /// <param name="key">The key of the item to retrieve.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if found; otherwise, the default value.</param>
        /// <returns>true if the key was found in the cache; otherwise, false.</returns>
        public bool TryGetValue(K key, out V value)
        {
            value = default;
            cacheLock.EnterUpgradeableReadLock();
            try
            {
                if (cacheMap.TryGetValue(key, out LinkedListNode<CacheItem> node))
                {
                    // Check if item has expired
                    if (ttlInMs > 0 && node.Value.IsExpired())
                    {
                        cacheLock.EnterWriteLock();
                        try
                        {
                            RemoveNode(node);
                        }
                        finally
                        {
                            cacheLock.ExitWriteLock();
                        }
                        return false;
                    }

                    cacheLock.EnterWriteLock();
                    try
                    {
                        // Move accessed node to the end of the list (most recently used)
                        lruList.Remove(node);
                        lruList.AddLast(node);
                    }
                    finally
                    {
                        cacheLock.ExitWriteLock();
                    }
                    value = node.Value.Value;
                    return true;
                }
                return false;
            }
            finally
            {
                cacheLock.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Adds or updates a key-value pair in the cache.
        /// </summary>
        /// <param name="key">The key of the item to add.</param>
        /// <param name="value">The value of the item to add.</param>
        public void Set(K key, V value)
        {
            cacheLock.EnterWriteLock();
            try
            {
                if (cacheMap.TryGetValue(key, out LinkedListNode<CacheItem> node))
                {
                    // Key exists, update the value and move node to the end
                    node.Value.Value = value;
                    node.Value.UpdateExpiry(ttlInMs);
                    lruList.Remove(node);
                    lruList.AddLast(node);
                }
                else
                {
                    // If capacity exceeded, remove the least recently used item
                    while (cacheMap.Count >= maxCapacity)
                    {
                        var lru = lruList.First;
                        if (lru != null)
                        {
                            RemoveNode(lru);
                        }
                    }

                    // Add new item to the cache
                    var newItem = new CacheItem(key, value, ttlInMs);
                    var newNode = new LinkedListNode<CacheItem>(newItem);
                    lruList.AddLast(newNode);
                    cacheMap[key] = newNode;
                }
            }
            finally
            {
                cacheLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Removes the item with the specified key from the cache.
        /// </summary>
        /// <param name="key"></param>
        public void Delete(K key) {
            cacheLock.EnterWriteLock();
            try {
                RemoveNode(cacheMap[key]);
            }
            catch {
                // Ignore if key not found
            }
            finally {
                cacheLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Clears the cache.
        /// </summary>
        public void Clear()
        {
            cacheMap.Clear();
            lruList.Clear();
        }

        /// <summary>
        /// Gets the keys of the cache.
        /// </summary>
        /// <returns>The keys of the cache.</returns>
        public IEnumerable<K> GetKeys()
        {
            return cacheMap.Keys;
        }

        private void RemoveNode(LinkedListNode<CacheItem> node)
        {
            lruList.Remove(node);
            cacheMap.Remove(node.Value.Key);
        }


        /// <summary>
        /// A private class to store items in the cache.
        /// </summary>
        private class CacheItem
        {
            public K Key { get; }
            public V Value { get; set; }
            private double ExpiryTime { get; set; }

            public CacheItem(K key, V value, int ttlInMs)
            {
                Key = key;
                Value = value;
                UpdateExpiry(ttlInMs);
            }

            public void UpdateExpiry(int ttlInMs)
            {
                ExpiryTime = ttlInMs > 0 ? GetCurrentTimestamp() + ttlInMs : 0;
            }

            public bool IsExpired()
            {
                return ExpiryTime > 0 && GetCurrentTimestamp() >= ExpiryTime;
            }

            private static double GetCurrentTimestamp()
            {
                return Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency * 1000;
            }
        }
    }
}
