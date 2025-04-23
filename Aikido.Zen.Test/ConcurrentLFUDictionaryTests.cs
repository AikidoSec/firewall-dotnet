using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks; // For potential concurrency tests later
using Aikido.Zen.Core.Models;
using NUnit.Framework;

namespace Aikido.Zen.Test
{
    /// <summary>
    /// Helper class for testing ConcurrentLFUDictionary.
    /// Inherits from HitCount.
    /// </summary>
    public class TestItem : HitCount
    {
        public string Value { get; set; }

        // Add a constructor that initializes Hits to 0 via base()
        public TestItem(string value) : base()
        {
            Value = value;
        }

        // Basic equality for testing purposes
        public override bool Equals(object obj)
        {
            // Compare Value only for simplicity in some tests, Hits checked separately
            return obj is TestItem item && Value == item.Value;
        }

        public override int GetHashCode()
        {
            // Hash based on Value only
            return HashCode.Combine(Value);
        }
    }

    [TestFixture]
    public class ConcurrentLFUDictionaryTests
    {
        [Test]
        public void Constructor_WithInvalidCapacity_ThrowsArgumentException()
        {
            // Capacity 0 should throw ArgumentException
            Assert.Throws<ArgumentException>(() => new ConcurrentLFUDictionary<string, TestItem>(0));
            // Negative capacity should also throw ArgumentException due to our check
            Assert.Throws<ArgumentException>(() => new ConcurrentLFUDictionary<string, TestItem>(-1));
        }

        [Test]
        public void AddOrUpdate_AddsNewItemSuccessfully_AndIncrementsHits()
        {
            var dict = new ConcurrentLFUDictionary<string, TestItem>(5);
            var item = new TestItem("value1");
            Assert.That(item.Hits, Is.EqualTo(0), "Initial hits should be 0");

            var returnedItem = dict.AddOrUpdate("key1", item);

            Assert.That(dict.Size, Is.EqualTo(1));
            Assert.That(dict.TryGet("key1", out var retrievedItem), Is.True);
            Assert.That(retrievedItem, Is.SameAs(item));
            Assert.That(retrievedItem.Value, Is.EqualTo("value1"));
            Assert.That(retrievedItem.Hits, Is.EqualTo(1), "Hits should be incremented to 1 on add");
            Assert.That(returnedItem, Is.SameAs(item));
            Assert.That(returnedItem.Hits, Is.EqualTo(1));
        }

        [Test]
        public void AddOrUpdate_UpdatesExistingItem_AndIncrementsHits()
        {
            var dict = new ConcurrentLFUDictionary<string, TestItem>(5);
            var item1 = new TestItem("value1");
            dict.AddOrUpdate("key1", item1); // Hits = 1

            var item2 = new TestItem("value2");
            Assert.That(item2.Hits, Is.EqualTo(0));

            var returnedItem = dict.AddOrUpdate("key1", item2); // Should replace item1, item2 hits = 1

            Assert.That(dict.Size, Is.EqualTo(1));
            Assert.That(dict.TryGet("key1", out var retrievedItem), Is.True);
            Assert.That(retrievedItem, Is.SameAs(item2)); // Should be the new item
            Assert.That(retrievedItem.Value, Is.EqualTo("value2"));
            Assert.That(retrievedItem.Hits, Is.EqualTo(1), "Hits of new item should be incremented to 1 on update");
            Assert.That(item1.Hits, Is.EqualTo(1), "Original item's hits should be unchanged");
            Assert.That(returnedItem, Is.SameAs(item2));
            Assert.That(returnedItem.Hits, Is.EqualTo(1));
        }

        [Test]
        public void TryGet_GetsItemWithoutIncrementingHits()
        {
            var dict = new ConcurrentLFUDictionary<string, TestItem>(5);
            var item = new TestItem("value1");
            dict.AddOrUpdate("key1", item); // Hits = 1

            bool found1 = dict.TryGet("key1", out var retrievedItem1);
            Assert.That(found1, Is.True);
            Assert.That(retrievedItem1, Is.SameAs(item));
            Assert.That(retrievedItem1.Hits, Is.EqualTo(1)); // Hit count unchanged by TryGet

            // Manually increment to simulate external usage
            retrievedItem1.Increment(); // Hits = 2
            Assert.That(item.Hits, Is.EqualTo(2));

            bool found2 = dict.TryGet("key1", out var retrievedItem2); // Get again
            Assert.That(found2, Is.True);
            Assert.That(retrievedItem2, Is.SameAs(item));
            Assert.That(retrievedItem2.Hits, Is.EqualTo(2)); // Hit count still unchanged by TryGet
        }

        [Test]
        public void TryGet_FailsForNonExistentKey()
        {
            var dict = new ConcurrentLFUDictionary<string, TestItem>(5);
            bool found = dict.TryGet("nonexistent", out var retrievedItem);
            Assert.That(found, Is.False);
            Assert.That(retrievedItem, Is.Null);
        }

        [Test]
        public void BasicEviction_RemovesLeastFrequentlyUsed_AfterAddOrUpdate()
        {
            var dict = new ConcurrentLFUDictionary<string, TestItem>(3); // Max 3 items

            var item1 = new TestItem("v1");
            var item2 = new TestItem("v2");
            var item3 = new TestItem("v3");

            // Add initial 3 items
            dict.AddOrUpdate("k1", item1); // k1 hits = 1, Size = 1
            dict.AddOrUpdate("k2", item2); // k2 hits = 1, Size = 2
            dict.AddOrUpdate("k3", item3); // k3 hits = 1, Size = 3

            // Update hits: k1=2, k2=3, k3=4
            dict.AddOrUpdate("k1", item1); // k1 hits = 2
            dict.AddOrUpdate("k2", item2); // k2 hits = 2
            dict.AddOrUpdate("k2", item2); // k2 hits = 3
            dict.AddOrUpdate("k3", item3); // k3 hits = 2
            dict.AddOrUpdate("k3", item3); // k3 hits = 3
            dict.AddOrUpdate("k3", item3); // k3 hits = 4
            // Current state: k1(2), k2(3), k3(4). Size=3.

            // Add 4th item. Since key k4 doesn't exist and Size (3) >= MaxItems (3),
            // LFU eviction happens *before* adding k4.
            // LFU item is k1 (Hits=2).
            var item4 = new TestItem("v4");
            dict.AddOrUpdate("k4", item4); // Evicts k1, Adds k4 (hits=1).

            // Final state: k2(3), k3(4), k4(1). Size=3.
            Assert.That(dict.Size, Is.EqualTo(3));
            Assert.That(dict.TryGet("k1", out _), Is.False, "k1 should have been evicted (LFU)");
            Assert.That(dict.TryGet("k2", out var i2), Is.True);
            Assert.That(i2.Hits, Is.EqualTo(3));
            Assert.That(dict.TryGet("k3", out var i3), Is.True);
            Assert.That(i3.Hits, Is.EqualTo(4));
            Assert.That(dict.TryGet("k4", out var i4), Is.True);
            Assert.That(i4.Hits, Is.EqualTo(1));
        }

        [Test]
        public void Eviction_WithTiedFrequency_RemovesOneOfTheLFUItems_AfterAddOrUpdate()
        {
            var dict = new ConcurrentLFUDictionary<string, TestItem>(3);

            var item1 = new TestItem("v1");
            var item2 = new TestItem("v2");
            var item3 = new TestItem("v3");

            dict.AddOrUpdate("k1", item1); // k1 hits = 1, Size = 1
            dict.AddOrUpdate("k2", item2); // k2 hits = 1, Size = 2
            dict.AddOrUpdate("k3", item3); // k3 hits = 1, Size = 3

            // Update k3 hits
            dict.AddOrUpdate("k3", item3); // k3 hits = 2
            dict.AddOrUpdate("k3", item3); // k3 hits = 3
            // Current state: k1(1), k2(1), k3(3). Size=3.

            // Add 4th item. Since key k4 doesn't exist and Size (3) >= MaxItems (3),
            // LFU eviction happens *before* adding k4.
            // LFU items are k1 (Hits=1) and k2 (Hits=1). One is evicted.
            var item4 = new TestItem("v4");
            dict.AddOrUpdate("k4", item4); // Evicts k1 or k2, Adds k4 (hits=1).

            // Final state: k3(3), k4(1), and one of {k1(1), k2(1)}. Size=3.
            Assert.That(dict.Size, Is.EqualTo(3));
            Assert.That(dict.TryGet("k3", out var i3), Is.True); // k3 should always remain
            Assert.That(i3.Hits, Is.EqualTo(3));
            Assert.That(dict.TryGet("k4", out var i4), Is.True); // k4 should always remain (added after eviction)
            Assert.That(i4.Hits, Is.EqualTo(1));

            bool k1Present = dict.TryGet("k1", out var i1);
            bool k2Present = dict.TryGet("k2", out var i2);

            Assert.That(k1Present ^ k2Present, Is.True, "Exactly one of the originally tied LFU items (k1 or k2) should have been evicted.");

            if (k1Present) Assert.That(i1.Hits, Is.EqualTo(1));
            if (k2Present) Assert.That(i2.Hits, Is.EqualTo(1));
            // Removed check for k4 presence as it's asserted above.
        }

        [Test]
        public void Delete_RemovesExistingItem() // Renamed from TryRemove test
        {
            var dict = new ConcurrentLFUDictionary<string, TestItem>(5);
            var item = new TestItem("value1");
            dict.AddOrUpdate("key1", item);

            bool deleted = dict.Delete("key1"); // Use Delete instead of TryRemove

            Assert.That(deleted, Is.True);
            Assert.That(dict.Size, Is.EqualTo(0));
            Assert.That(dict.TryGet("key1", out _), Is.False); // Check via TryGet
        }

        [Test]
        public void Delete_FailsForNonExistentKey() // Renamed from TryRemove test
        {
            var dict = new ConcurrentLFUDictionary<string, TestItem>(5);
            dict.AddOrUpdate("key1", new TestItem("value1"));

            bool deleted = dict.Delete("nonexistent"); // Use Delete

            Assert.That(deleted, Is.False);
            Assert.That(dict.Size, Is.EqualTo(1));
        }

        [Test]
        public void Clear_RemovesAllItems()
        {
            var dict = new ConcurrentLFUDictionary<string, TestItem>(5);
            dict.AddOrUpdate("key1", new TestItem("value1"));
            dict.AddOrUpdate("key2", new TestItem("value2"));
            Assert.That(dict.Size, Is.EqualTo(2));

            dict.Clear();

            Assert.That(dict.Size, Is.EqualTo(0));
            Assert.That(dict.GetKeys(), Is.Empty);
            Assert.That(dict.TryGet("key1", out _), Is.False);
            Assert.That(dict.TryGet("key2", out _), Is.False);
        }

        [Test]
        public void AddOrUpdate_ShouldEvictLeastFrequentlyUsed_WhenMaxReached() // Renamed test
        {
            var dict = new ConcurrentLFUDictionary<string, TestItem>(3); // Max 3 items

            var item1 = new TestItem("user1");
            var item2 = new TestItem("user2");
            var item3 = new TestItem("user3");

            dict.AddOrUpdate("user1", item1); // user1 hits = 1, Size = 1
            dict.AddOrUpdate("user2", item2); // user2 hits = 1, Size = 2
            dict.AddOrUpdate("user3", item3); // user3 hits = 1, Size = 3

            // Update hits
            dict.AddOrUpdate("user1", item1); // user1 hits = 2
            dict.AddOrUpdate("user2", item2); // user2 hits = 2

            // Pre-eviction hits: user1=2, user2=2, user3=1
            Assert.That(item1.Hits, Is.EqualTo(2));
            Assert.That(item2.Hits, Is.EqualTo(2));
            Assert.That(item3.Hits, Is.EqualTo(1));

            // Add 4th item, triggering eviction
            var item4 = new TestItem("user4");
            dict.AddOrUpdate("user4", item4); // user4 added, hits=1. Size becomes 4, triggers eviction.
                                              // Current hits: user1=2, user2=2, user3=1, user4=1. LFU is user3 or user4.

            Assert.That(dict.Size, Is.EqualTo(3));
            Assert.That(dict.TryGet("user1", out var i1), Is.True); // Should remain
            Assert.That(i1.Hits, Is.EqualTo(2));
            Assert.That(dict.TryGet("user2", out var i2), Is.True); // Should remain
            Assert.That(i2.Hits, Is.EqualTo(2));

            bool user3Present = dict.TryGet("user3", out var i3);
            bool user4Present = dict.TryGet("user4", out var i4);

            Assert.That(user3Present ^ user4Present, Is.True, "Exactly one of the LFU items (user3 or user4) should have been evicted.");

            if (user3Present) Assert.That(i3.Hits, Is.EqualTo(1));
            if (user4Present) Assert.That(i4.Hits, Is.EqualTo(1));
        }

        [Test]
        public async Task ConcurrentAddOrUpdateAndGet_MaintainsConsistency() // Renamed test
        {
            var dict = new ConcurrentLFUDictionary<string, TestItem>(100); // Larger capacity for concurrency test
            const int numTasks = 10;
            const int iterations = 100;
            var keys = Enumerable.Range(0, 10).Select(i => $"key{i}").ToList(); // Fewer keys, more contention

            var tasks = new List<Task>();

            for (int i = 0; i < numTasks; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    var random = new Random(Thread.CurrentThread.ManagedThreadId);
                    for (int j = 0; j < iterations; j++)
                    {
                        string key = keys[random.Next(keys.Count)];
                        int action = random.Next(2); // 0 = AddOrUpdate, 1 = TryGet

                        if (action == 0)
                        {
                            // Use existing item instance if present, otherwise new one, to simulate updates vs adds
                            // This is tricky as AddOrUpdate replaces the instance.
                            // Let's simplify: always "update" with a new instance, AddOrUpdate handles add/replace.
                            var newItem = new TestItem($"value_{key}_{j}_{Thread.CurrentThread.ManagedThreadId}");
                            dict.AddOrUpdate(key, newItem);
                        }
                        else
                        {
                            // Just try to get, don't care about the result for this consistency test
                            dict.TryGet(key, out _);
                        }
                        // Small delay to increase chance of interleaving
                        // Task.Delay(random.Next(1, 5)).Wait(); removed for faster test
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Basic assertions: Size should be <= number of unique keys used.
            // Hit counts should reflect multiple AddOrUpdate calls.
            Assert.That(dict.Size, Is.LessThanOrEqualTo(keys.Count));

            int totalExpectedHits = numTasks * iterations; // Rough estimate, depends on action distribution
            long actualTotalHits = 0;
            foreach (var key in dict.GetKeys()) // Use GetKeys()
            {
                if (dict.TryGet(key, out var item))
                {
                    actualTotalHits += item.Hits;
                    Assert.That(item.Hits, Is.GreaterThan(0)); // Each remaining item should have been hit at least once by AddOrUpdate
                }
            }
            // Cannot reliably assert exact total hits due to randomness and potential eviction (if capacity was smaller)
            Console.WriteLine($"Final dictionary size: {dict.Size}");
            Console.WriteLine($"Final total hits across items: {actualTotalHits}");
        }

        // Add test for GetKeys and GetValues
        [Test]
        public void GetKeys_ReturnsAllCurrentKeys()
        {
            var dict = new ConcurrentLFUDictionary<string, TestItem>(5);
            dict.AddOrUpdate("k1", new TestItem("v1"));
            dict.AddOrUpdate("k2", new TestItem("v2"));
            dict.AddOrUpdate("k3", new TestItem("v3"));

            var keys = dict.GetKeys().ToList();

            Assert.That(keys.Count, Is.EqualTo(3));
            Assert.That(keys, Is.EquivalentTo(new[] { "k1", "k2", "k3" }));
        }

        [Test]
        public void GetValues_ReturnsAllCurrentValues()
        {
            var dict = new ConcurrentLFUDictionary<string, TestItem>(5);
            var v1 = new TestItem("v1");
            var v2 = new TestItem("v2");
            var v3 = new TestItem("v3");
            dict.AddOrUpdate("k1", v1);
            dict.AddOrUpdate("k2", v2);
            dict.AddOrUpdate("k3", v3);

            var values = dict.GetValues().ToList();

            Assert.That(values.Count, Is.EqualTo(3));
            Assert.That(values, Is.EquivalentTo(new[] { v1, v2, v3 })); // Check instance equality equivalence
            Assert.That(values.Select(v => v.Value), Is.EquivalentTo(new[] { "v1", "v2", "v3" }));
        }
    }
}
