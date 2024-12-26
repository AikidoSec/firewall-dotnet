using Aikido.Zen.Core.Models;
using NUnit.Framework;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace Aikido.Zen.Test
{
    public class LRUCacheTests
    {
        private LRUCache<string, string> _cache;

        [SetUp]
        public void Setup()
        {
            _cache = new LRUCache<string, string>(3, 100); // Capacity 3, TTL 100ms
        }

        [Test]
        public void Constructor_WithInvalidCapacity_ShouldThrowArgumentException()
        {
            Assert.That(() => new LRUCache<string, string>(-1), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Constructor_WithInvalidTTL_ShouldThrowArgumentException()
        {
            Assert.That(() => new LRUCache<string, string>(100, -1), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Constructor_NewCache_ShouldHaveZeroSize()
        {
            var cache = new LRUCache<int, string>(5, 1000);
            Assert.That(cache.Size, Is.EqualTo(0));
        }

        [Test]
        public void SetAndTryGetValue_BasicOperations_ShouldWork()
        {
            // Act
            _cache.Set("key1", "value1");
            _cache.Set("key2", "value2");

            // Assert
            string value1, value2;
            Assert.That(_cache.TryGetValue("key1", out value1), Is.True);
            Assert.That(value1, Is.EqualTo("value1"));
            Assert.That(_cache.TryGetValue("key2", out value2), Is.True);
            Assert.That(value2, Is.EqualTo("value2"));
            Assert.That(_cache.Size, Is.EqualTo(2));
        }

        [Test]
        public void Set_WhenOverCapacity_ShouldEvictLRUItem()
        {
            // Arrange
            var cache = new LRUCache<int, string>(2);

            // Act
            cache.Set(1, "value1");
            cache.Set(2, "value2");
            Assert.That(cache.Size, Is.EqualTo(2));

            cache.Set(3, "value3");

            // Assert
            Assert.That(cache.Size, Is.EqualTo(2));
            string value1, value2, value3;
            Assert.That(cache.TryGetValue(1, out value1), Is.False);
            Assert.That(cache.TryGetValue(2, out value2), Is.True);
            Assert.That(value2, Is.EqualTo("value2"));
            Assert.That(cache.TryGetValue(3, out value3), Is.True);
            Assert.That(value3, Is.EqualTo("value3"));
        }

        [Test]
        public async Task TryGetValue_AfterTTLExpiration_ShouldReturnFalse()
        {
            // Arrange
            var cache = new LRUCache<string, string>(5, 100);

            // Act
            cache.Set("key1", "value1");
            string value;
            Assert.That(cache.TryGetValue("key1", out value), Is.True);
            Assert.That(value, Is.EqualTo("value1"));

            await Task.Delay(150); // Wait for TTL to expire

            // Assert
            Assert.That(cache.TryGetValue("key1", out value), Is.False);
            Assert.That(cache.Size, Is.EqualTo(0));
        }

        [Test]
        public void Clear_ShouldRemoveAllItems()
        {
            // Arrange
            _cache.Set("key1", "value1");
            _cache.Set("key2", "value2");
            Assert.That(_cache.Size, Is.EqualTo(2));

            // Act
            _cache.Clear();

            // Assert
            Assert.That(_cache.Size, Is.EqualTo(0));
            string value;
            Assert.That(_cache.TryGetValue("key1", out value), Is.False);
            Assert.That(_cache.TryGetValue("key2", out value), Is.False);
        }

        [Test]
        public void Delete_ShouldRemoveSpecificItem()
        {
            // Arrange
            _cache.Set("key1", "value1");
            _cache.Set("key2", "value2");
            Assert.That(_cache.Size, Is.EqualTo(2));

            // Act
            _cache.Delete("key1");

            // Assert
            Assert.That(_cache.Size, Is.EqualTo(1));
            string value1, value2;
            Assert.That(_cache.TryGetValue("key1", out value1), Is.False);
            Assert.That(_cache.TryGetValue("key2", out value2), Is.True);
            Assert.That(value2, Is.EqualTo("value2"));
        }

        [Test]
        public void GetKeys_ShouldReturnAllKeys()
        {
            // Arrange
            _cache.Set("key1", "value1");
            _cache.Set("key2", "value2");
            _cache.Set("key3", "value3");

            // Act
            var keys = _cache.GetKeys().ToList();

            // Assert
            Assert.That(keys, Is.EquivalentTo(new[] { "key1", "key2", "key3" }));
        }

        [Test]
        public void ConcurrentAccess_ShouldHandleMultipleThreads()
        {
            // Arrange
            var tasks = new List<Task>();
            var cache = new LRUCache<int, int>(100);

            // Act & Assert
            Assert.That(async () => {
                for (int i = 0; i < 100; i++)
                {
                    int value = i;
                    tasks.Add(Task.Run(() => cache.Set(value, value)));
                    tasks.Add(Task.Run(() => {
                        int outValue;
                        cache.TryGetValue(value, out outValue);
                    }));
                }
                await Task.WhenAll(tasks);
            }, Throws.Nothing);
        }

        [Test]
        public void TryGetValue_NonExistentKey_ShouldReturnFalse()
        {
            string value;
            Assert.That(_cache.TryGetValue("nonexistent", out value), Is.False);
            Assert.That(value, Is.EqualTo(default(string)));
        }

        [Test]
        public void Set_ExistingKey_ShouldUpdateValue()
        {
            // Arrange
            _cache.Set("key1", "value1");
            
            // Act
            _cache.Set("key1", "updatedValue");
            
            // Assert
            string value;
            Assert.That(_cache.TryGetValue("key1", out value), Is.True);
            Assert.That(value, Is.EqualTo("updatedValue"));
        }

        [Test]
        public void Delete_NonExistentKey_ShouldNotThrowException()
        {
            Assert.That(() => _cache.Delete("nonexistent"), Throws.Nothing);
        }

        [Test]
        public void GetKeys_EmptyCache_ShouldReturnEmptyCollection()
        {
            Assert.That(_cache.GetKeys(), Is.Empty);
        }

        [Test]
        public void Set_UpdateExistingKeyWithTTL_ShouldResetExpiration()
        {
            // Arrange
            var cache = new LRUCache<string, string>(5, 200);
            cache.Set("key1", "value1");
            
            Task.Delay(100).Wait(); // Wait half the TTL
            
            // Act
            cache.Set("key1", "value2"); // Should reset TTL
            Task.Delay(150).Wait(); // Wait longer than original TTL but less than reset TTL
            
            // Assert
            string value;
            Assert.That(cache.TryGetValue("key1", out value), Is.True);
            Assert.That(value, Is.EqualTo("value2"));
        }

        [Test]
        public void TryGetValue_WithZeroTTL_ShouldNeverExpire()
        {
            // Arrange
            var cache = new LRUCache<string, string>(5, 0); // TTL = 0 means no expiration
            cache.Set("key1", "value1");
            
            // Act
            Task.Delay(200).Wait(); // Wait some time
            
            // Assert
            string value;
            Assert.That(cache.TryGetValue("key1", out value), Is.True);
            Assert.That(value, Is.EqualTo("value1"));
        }
    }
}
