using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Test
{
    public class RateLimitingHelperTests
    {
        [SetUp]
        public void Setup()
        {
            // Reset cache before each test with default values
            RateLimitingHelper.ResetCache(10000, 120 * 60 * 1000);
        }

        [Test]
        public void IsAllowed_ShouldAllowUpToMaxRequestsWithinWindow()
        {
            // Arrange
            string key = "test_key";
            int windowSize = 60000; // 1 minute
            int maxRequests = 5;

            // Act & Assert
            for (int i = 0; i < maxRequests; i++)
            {
                Assert.That(RateLimitingHelper.IsAllowed(key, windowSize, maxRequests), Is.True, 
                    $"Request {i + 1} should be allowed");
            }

            Assert.That(RateLimitingHelper.IsAllowed(key, windowSize, maxRequests), Is.False,
                $"Request {maxRequests + 1} should not be allowed");
        }

        [Test]
        public void IsAllowed_ShouldResetAfterWindowExpires()
        {
            // Arrange
            string key = "test_key";
            int windowSize = 100; // Small window for testing
            int maxRequests = 5;

            // Act & Assert
            // Use up all allowed requests
            for (int i = 0; i < maxRequests; i++)
            {
                Assert.That(RateLimitingHelper.IsAllowed(key, windowSize, maxRequests), Is.True,
                    $"Request {i + 1} should be allowed");
            }

            Assert.That(RateLimitingHelper.IsAllowed(key, windowSize, maxRequests), Is.False,
                $"Request {maxRequests + 1} should not be allowed");

            // Wait for window to expire
            System.Threading.Thread.Sleep(windowSize + 50);

            // Should allow requests again after window expires
            Assert.That(RateLimitingHelper.IsAllowed(key, windowSize, maxRequests), Is.True,
                "Request after window expiry should be allowed");
        }

        [Test]
        public void IsAllowed_ShouldTrackDifferentKeysIndependently()
        {
            // Arrange
            string key1 = "user1";
            string key2 = "user2"; 
            int windowSize = 60000; // 1 minute
            int maxRequests = 5;

            // Act & Assert
            // Use up all requests for key1
            for (int i = 0; i < maxRequests; i++)
            {
                Assert.That(RateLimitingHelper.IsAllowed(key1, windowSize, maxRequests), Is.True,
                    $"Request {i + 1} for key1 should be allowed");
            }
            Assert.That(RateLimitingHelper.IsAllowed(key1, windowSize, maxRequests), Is.False,
                $"Request {maxRequests + 1} for key1 should not be allowed");

            // key2 should still have all requests available
            for (int i = 0; i < maxRequests; i++)
            {
                Assert.That(RateLimitingHelper.IsAllowed(key2, windowSize, maxRequests), Is.True,
                    $"Request {i + 1} for key2 should be allowed");
            }
            Assert.That(RateLimitingHelper.IsAllowed(key2, windowSize, maxRequests), Is.False,
                $"Request {maxRequests + 1} for key2 should not be allowed");
        }

        [Test]
        public void IsAllowed_WithZeroMaxRequests_ShouldAlwaysDeny()
        {
            // Arrange
            string key = "zero_requests";
            int windowSize = 1000;
            int maxRequests = 0;

            // Act & Assert
            Assert.That(RateLimitingHelper.IsAllowed(key, windowSize, maxRequests), Is.False);
        }

        [Test]
        public void IsAllowed_WithNegativeWindowSize_ShouldTreatAsExpired()
        {
            // Arrange
            string key = "negative_window";
            int windowSize = -1000;
            int maxRequests = 5;

            // Act & Assert
            Assert.That(RateLimitingHelper.IsAllowed(key, windowSize, maxRequests), Is.True);
            Assert.That(RateLimitingHelper.IsAllowed(key, windowSize, maxRequests), Is.True);
        }

        [Test]
        public void ConcurrentAccess_ShouldHandleMultipleThreads()
        {
            // Arrange
            var tasks = new List<Task>();
            string key = "concurrent_key";
            int windowSize = 1000;
            int maxRequests = 100;
            int totalAttempts = 200;

            // Act
            for (int i = 0; i < totalAttempts; i++)
            {
                tasks.Add(Task.Run(() => RateLimitingHelper.IsAllowed(key, windowSize, maxRequests)));
            }
            Task.WaitAll(tasks.ToArray());

            // Assert
            Assert.That(RateLimitingHelper.IsAllowed(key, windowSize, maxRequests), Is.False);
        }

        [Test]
        public void Cache_WithDifferentSizes_ShouldRespectCapacity()
        {
            // Arrange
            RateLimitingHelper.ResetCache(2, 1000); // Small cache size
            int windowSize = 1000;
            int maxRequests = 5;

            // Act
            RateLimitingHelper.IsAllowed("key1", windowSize, maxRequests);
            RateLimitingHelper.IsAllowed("key2", windowSize, maxRequests);
            RateLimitingHelper.IsAllowed("key3", windowSize, maxRequests); // Should evict key1

            // Assert - key1 should be treated as new request since it was evicted
            Assert.That(RateLimitingHelper.IsAllowed("key1", windowSize, maxRequests), Is.True);
        }

        [Test]
        public void IsAllowed_WithNegativeMaxRequests_ShouldAlwaysDeny()
        {
            // Arrange
            string key = "negative_requests";
            int windowSize = 1000;
            int maxRequests = -5;

            // Act & Assert
            Assert.That(RateLimitingHelper.IsAllowed(key, windowSize, maxRequests), Is.False);
        }

        [Test]
        public void IsAllowed_WithZeroWindowSize_ShouldAlwaysAllow()
        {
            // Arrange
            string key = "zero_window";
            int windowSize = 0;
            int maxRequests = 5;

            // Act & Assert
            for (int i = 0; i < maxRequests + 5; i++) // Test more than maxRequests
            {
                Assert.That(RateLimitingHelper.IsAllowed(key, windowSize, maxRequests), Is.True,
                    $"Request {i + 1} should be allowed with zero window size");
            }
        }

        [Test]
        public void IsAllowed_ShouldIncrementCounterCorrectly()
        {
            // Arrange
            string key = "increment_test";
            int windowSize = 1000;
            int maxRequests = 3;

            // Act & Assert
            // First request - should set initial count to 1
            Assert.That(RateLimitingHelper.IsAllowed(key, windowSize, maxRequests), Is.True);
            
            // Second request - should increment to 2
            Assert.That(RateLimitingHelper.IsAllowed(key, windowSize, maxRequests), Is.True);
            
            // Third request - should increment to 3
            Assert.That(RateLimitingHelper.IsAllowed(key, windowSize, maxRequests), Is.True);
            
            // Fourth request - should be denied as count is at max
            Assert.That(RateLimitingHelper.IsAllowed(key, windowSize, maxRequests), Is.False);
        }
    }
}
