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
    /// Inherits from HitCount, starting Hits at 1.
    /// </summary>
    public class TestItem : HitCount
    {
        public string Value { get; set; }

        public TestItem(string value) : base()
        {
            Value = value;
        }

        // Basic equality for testing purposes
        public override bool Equals(object obj)
        {
            return obj is TestItem item && Value == item.Value && Hits == item.Hits;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Value, Hits);
        }
    }

    [TestFixture]
    public class ConcurrentLFUDictionaryTests
    {
        [Test]
        public void Constructor_WithInvalidCapacity_ThrowsCorrectException()
        {
            // Capacity 0 should throw ArgumentException from our check
            Assert.Throws<ArgumentException>(() => new ConcurrentLFUDictionary<string, TestItem>(0));
            // Negative capacity should throw ArgumentOutOfRangeException from the base ConcurrentDictionary constructor
            Assert.Throws<ArgumentOutOfRangeException>(() => new ConcurrentLFUDictionary<string, TestItem>(-1));
        }

        [Test]
        public void TryAdd_AddsItemSuccessfully()
        {
            var dict = new ConcurrentLFUDictionary<string, TestItem>(5);
            var item = new TestItem("value1");
            bool added = dict.TryAdd("key1", item);

            Assert.That(added, Is.True);
            Assert.That(dict.Count, Is.EqualTo(1));
            Assert.That(dict.ContainsKey("key1"), Is.True);
            Assert.That(dict["key1"].Hits, Is.EqualTo(1));
        }

        [Test]
        public void TryAdd_FailsIfKeyExists()
        {
            var dict = new ConcurrentLFUDictionary<string, TestItem>(5);
            dict.TryAdd("key1", new TestItem("value1"));
            bool added = dict.TryAdd("key1", new TestItem("value2"));

            Assert.That(added, Is.False);
            Assert.That(dict.Count, Is.EqualTo(1));
            Assert.That(dict["key1"].Value, Is.EqualTo("value1"));
        }

        [Test]
        public void TryGetValue_GetsItemWithoutIncrementingHits()
        {
            var dict = new ConcurrentLFUDictionary<string, TestItem>(5);
            var item = new TestItem("value1");
            dict.TryAdd("key1", item);

            bool found1 = dict.TryGetValue("key1", out var retrievedItem1);
            Assert.That(found1, Is.True);
            Assert.That(retrievedItem1.Value, Is.EqualTo("value1"));
            Assert.That(retrievedItem1.Hits, Is.EqualTo(1));
            Assert.That(dict["key1"].Hits, Is.EqualTo(1));

            retrievedItem1.Increment();
            Assert.That(dict["key1"].Hits, Is.EqualTo(2));

            bool found2 = dict.TryGetValue("key1", out var retrievedItem2);
            Assert.That(found2, Is.True);
            Assert.That(retrievedItem2.Hits, Is.EqualTo(2));
            Assert.That(dict["key1"].Hits, Is.EqualTo(2));
        }

        [Test]
        public void Indexer_Get_GetsItemWithoutIncrementingHits()
        {
            var dict = new ConcurrentLFUDictionary<string, TestItem>(5);
            var item = new TestItem("value1");
            dict.TryAdd("key1", item);

            var retrievedItem1 = dict["key1"];
            Assert.That(retrievedItem1.Value, Is.EqualTo("value1"));
            Assert.That(retrievedItem1.Hits, Is.EqualTo(1));

            retrievedItem1.Increment();
            Assert.That(dict["key1"].Hits, Is.EqualTo(2));

            var retrievedItem2 = dict["key1"];
            Assert.That(retrievedItem2.Value, Is.EqualTo("value1"));
            Assert.That(retrievedItem2.Hits, Is.EqualTo(2));
        }


        [Test]
        public void Indexer_Set_OverwritesItemAndResetsHits()
        {
            var dict = new ConcurrentLFUDictionary<string, TestItem>(5);
            var originalItem = new TestItem("value1");
            dict.TryAdd("key1", originalItem);
            originalItem.Increment();

            var newItem = new TestItem("value2");
            dict["key1"] = newItem;

            Assert.That(dict.Count, Is.EqualTo(1));
            Assert.That(dict["key1"].Value, Is.EqualTo("value2"));
            Assert.That(dict["key1"].Hits, Is.EqualTo(1));
        }

        [Test]
        public void Set_OverwritesItemAndResetsHits()
        {
            var dict = new ConcurrentLFUDictionary<string, TestItem>(5);
            var originalItem = new TestItem("value1");
            dict.TryAdd("key1", originalItem);
            originalItem.Increment();

            var newItem = new TestItem("value2");
            dict.Set("key1", newItem);

            Assert.That(dict.Count, Is.EqualTo(1));
            Assert.That(dict["key1"].Value, Is.EqualTo("value2"));
            Assert.That(dict["key1"].Hits, Is.EqualTo(1));
        }

        [Test]
        public void BasicEviction_RemovesLeastFrequentlyUsed_WithExplicitIncrements()
        {
            var dict = new ConcurrentLFUDictionary<string, TestItem>(3);

            var item1 = new TestItem("v1");
            var item2 = new TestItem("v2");
            var item3 = new TestItem("v3");

            dict.TryAdd("k1", item1);
            dict.TryAdd("k2", item2);
            dict.TryAdd("k3", item3);

            if (dict.TryGetValue("k1", out var retrievedItem1))
            {
                retrievedItem1.Increment();
            }
            if (dict.TryGetValue("k2", out var retrievedItem2))
            {
                retrievedItem2.Increment();
                retrievedItem2.Increment();
            }
            if (dict.TryGetValue("k3", out var retrievedItem3))
            {
                retrievedItem3.Increment();
                retrievedItem3.Increment();
                retrievedItem3.Increment();
            }

            var item4 = new TestItem("v4");
            dict.TryAdd("k4", item4);

            Assert.That(dict.Count, Is.EqualTo(3));
            Assert.That(dict.ContainsKey("k1"), Is.True);
            Assert.That(dict.ContainsKey("k2"), Is.True);
            Assert.That(dict.ContainsKey("k3"), Is.True);
            Assert.That(dict.ContainsKey("k4"), Is.False, "k4 should have been evicted (LFU)");
            Assert.That(dict["k1"].Hits, Is.EqualTo(2));
            Assert.That(dict["k2"].Hits, Is.EqualTo(3));
            Assert.That(dict["k3"].Hits, Is.EqualTo(4));
        }

        [Test]
        public void Eviction_WithTiedFrequency_RemovesOneOfTheLFUItems_WithExplicitIncrements()
        {
            var dict = new ConcurrentLFUDictionary<string, TestItem>(3);

            var item1 = new TestItem("v1");
            var item2 = new TestItem("v2");
            var item3 = new TestItem("v3");

            dict.TryAdd("k1", item1);
            dict.TryAdd("k2", item2);
            dict.TryAdd("k3", item3);

            if (dict.TryGetValue("k3", out var retrievedItem3))
            {
                retrievedItem3.Increment();
                retrievedItem3.Increment();
            }

            var item4 = new TestItem("v4");
            dict.TryAdd("k4", item4);

            Assert.That(dict.Count, Is.EqualTo(3));
            Assert.That(dict.ContainsKey("k3"), Is.True);
            Assert.That(dict.ContainsKey("k4"), Is.True);
            bool k1Present = dict.ContainsKey("k1");
            bool k2Present = dict.ContainsKey("k2");
            Assert.That(k1Present ^ k2Present, Is.True, "Exactly one of the tied LFU items (k1 or k2) should have been evicted.");

            if (k1Present)
            {
                Assert.That(dict["k1"].Hits, Is.EqualTo(1));
            }
            if (k2Present)
            {
                Assert.That(dict["k2"].Hits, Is.EqualTo(1));
            }
            Assert.That(dict["k3"].Hits, Is.EqualTo(3));
            Assert.That(dict["k4"].Hits, Is.EqualTo(1));
        }

        [Test]
        public void TryRemove_RemovesExistingItem()
        {
            var dict = new ConcurrentLFUDictionary<string, TestItem>(5);
            var item = new TestItem("value1");
            dict.TryAdd("key1", item);

            bool removed = dict.TryRemove("key1", out var removedItem);

            Assert.That(removed, Is.True);
            Assert.That(removedItem?.Value, Is.EqualTo("value1"));
            Assert.That(dict.Count, Is.EqualTo(0));
            Assert.That(dict.ContainsKey("key1"), Is.False);
        }

        [Test]
        public void TryRemove_FailsForNonExistentKey()
        {
            var dict = new ConcurrentLFUDictionary<string, TestItem>(5);
            dict.TryAdd("key1", new TestItem("value1"));

            bool removed = dict.TryRemove("keyNonExistent", out var removedItem);

            Assert.That(removed, Is.False);
            Assert.That(removedItem, Is.Null);
            Assert.That(dict.Count, Is.EqualTo(1));
            Assert.That(dict.ContainsKey("key1"), Is.True);
        }

        [Test]
        public void Delete_RemovesExistingItem()
        {
            var dict = new ConcurrentLFUDictionary<string, TestItem>(5);
            var item = new TestItem("value1");
            dict.TryAdd("key1", item);

            bool deleted = dict.Delete("key1");

            Assert.That(deleted, Is.True);
            Assert.That(dict.Count, Is.EqualTo(0));
            Assert.That(dict.ContainsKey("key1"), Is.False);
        }

        [Test]
        public void Delete_FailsForNonExistentKey()
        {
            var dict = new ConcurrentLFUDictionary<string, TestItem>(5);
            dict.TryAdd("key1", new TestItem("value1"));

            bool deleted = dict.Delete("keyNonExistent");

            Assert.That(deleted, Is.False);
            Assert.That(dict.Count, Is.EqualTo(1));
            Assert.That(dict.ContainsKey("key1"), Is.True);
        }

        [Test]
        public void Clear_RemovesAllItems()
        {
            var dict = new ConcurrentLFUDictionary<string, TestItem>(5);
            dict.TryAdd("key1", new TestItem("value1"));
            dict.TryAdd("key2", new TestItem("value2"));

            Assert.That(dict.Count, Is.EqualTo(2));

            dict.Clear();

            Assert.That(dict.Count, Is.EqualTo(0));
            Assert.That(dict.IsEmpty, Is.True);
            Assert.That(dict.ContainsKey("key1"), Is.False);
            Assert.That(dict.ContainsKey("key2"), Is.False);
        }

        [Test]
        public void AddUser_ShouldEvictLeastFrequentlyUsed_WhenMaxReached()
        {
            // Setup a dictionary with a specific capacity
            const int MaxUsers = 2000;
            var dict = new ConcurrentLFUDictionary<string, TestItem>(MaxUsers);

            // Add user0 with hit count = 1
            dict.TryAdd("user0", new TestItem("user0"));

            // Add users 1 through MaxUsers+1 with hit count = 2
            for (int i = 1; i <= MaxUsers + 1; i++)
            {
                string key = $"user{i}";
                var item = new TestItem(key);
                dict.TryAdd(key, item);

                if (dict.TryGetValue(key, out var retrievedItem))
                {
                    retrievedItem.Increment(); // Make hit count = 2
                }
            }

            // Verify dictionary size is at max capacity
            Assert.That(dict.Count, Is.EqualTo(MaxUsers));

            // Verify user0 was evicted (lowest hit count = 1)
            Assert.That(dict.ContainsKey("user0"), Is.False);

            // Verify the last added user is present (hit count = 2)
            Assert.That(dict.ContainsKey($"user{MaxUsers + 1}"), Is.True);

            if (dict.TryGetValue($"user{MaxUsers + 1}", out var lastUser))
            {
                Assert.That(lastUser.Hits, Is.EqualTo(2));
            }
        }

        [Test]
        public void ConcurrentAddGetAndIncrement_MaintainsConsistency()
        {
            var dict = new ConcurrentLFUDictionary<string, TestItem>(1000);
            int numOperations = 5000;
            int numThreads = 10;

            var tasks = new List<Task>();
            var random = new Random();

            for (int i = 0; i < numThreads; i++)
            {
                int threadId = i;
                tasks.Add(Task.Run(() =>
                {
                    var threadItems = new List<TestItem>();
                    for (int k = 0; k < 10; k++)
                    {
                        string initialKey = $"key_{threadId}_initial_{k}";
                        var initialItem = new TestItem($"value_{threadId}_initial_{k}");
                        if (dict.TryAdd(initialKey, initialItem))
                        {
                            threadItems.Add(initialItem);
                        }
                    }

                    for (int j = 0; j < numOperations; j++)
                    {
                        string key = $"key_{threadId}_{j}";
                        var item = new TestItem($"value_{threadId}_{j}");

                        int operationType;
                        lock (random)
                        {
                            operationType = random.Next(4);
                        }

                        if (operationType == 0)
                        {
                            if (dict.TryAdd(key, item))
                            {
                                lock (threadItems) threadItems.Add(item);
                            }
                        }
                        else if (operationType == 1)
                        {
                            int otherThreadId;
                            int itemIndex;
                            lock (random)
                            {
                                otherThreadId = random.Next(numThreads);
                                itemIndex = random.Next(numOperations);
                            }
                            string keyToGet = $"key_{otherThreadId}_{itemIndex}";
                            dict.TryGetValue(keyToGet, out _);
                        }
                        else if (operationType == 2)
                        {
                            dict.TryGetValue(key, out _);
                        }
                        else
                        {
                            TestItem itemToIncrement = null;
                            lock (threadItems)
                            {
                                if (threadItems.Count > 0)
                                {
                                    itemToIncrement = threadItems[random.Next(threadItems.Count)];
                                }
                            }
                            itemToIncrement?.Increment();
                        }
                    }
                }));
            }

            try
            {
                Task.WaitAll(tasks.ToArray());
            }
            catch (AggregateException ae)
            {
                ae.Handle(ex =>
                {
                    Console.WriteLine($"Concurrency task failed: {ex}");
                    return true;
                });
                Assert.Fail("One or more concurrency tasks failed.");
            }

            int finalCount = dict.Count;
            Console.WriteLine($"ConcurrentLFUDictionary final count: {finalCount}");
            Assert.That(finalCount, Is.GreaterThanOrEqualTo(0).And.LessThanOrEqualTo(1000));
            Assert.That(() => dict.Values.ToList(), Throws.Nothing);
        }
    }
}
