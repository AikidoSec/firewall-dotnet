using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core;

namespace Aikido.Zen.Test.Helpers
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

        [Test]
        public void ShouldHandleTTLExpiration()
        {
            // Arrange
            string key = "test_key";
            int windowSize = 1000;
            int maxRequests = 5;

            // Act & Assert
            for (int i = 0; i < maxRequests; i++)
            {
                Assert.That(RateLimitingHelper.IsAllowed(key, windowSize, maxRequests), Is.True);
            }

            System.Threading.Thread.Sleep(windowSize + 50);

            Assert.That(RateLimitingHelper.IsAllowed(key, windowSize, maxRequests), Is.True,
                "Request after TTL should be allowed");
        }

        [Test]
        public void ShouldHandleSlidingWindowWithIntermittentRequests()
        {
            // Arrange
            string key = "test_key";
            int windowSize = 1000;
            int maxRequests = 5;
            int delayBetweenRequests = 100;

            // Act & Assert
            for (int i = 0; i < maxRequests; i++)
            {
                Assert.That(RateLimitingHelper.IsAllowed(key, windowSize, maxRequests), Is.True,
                    $"Request {i + 1} should be allowed");
                System.Threading.Thread.Sleep(delayBetweenRequests);
            }

            System.Threading.Thread.Sleep(windowSize + 50);

            Assert.That(RateLimitingHelper.IsAllowed(key, windowSize, maxRequests), Is.True,
                "Request after sliding window should be allowed");
        }

        [Test]
        public void ShouldHandleSlidingWindowWithBurstRequests()
        {
            // Arrange
            string key = "test_key";
            int windowSize = 1000;
            int maxRequests = 5;

            // Initial burst of requests
            for (int i = 0; i < maxRequests; i++)
            {
                Assert.That(RateLimitingHelper.IsAllowed(key, windowSize, maxRequests), Is.True,
                    $"Initial request {i + 1} should be allowed");
            }

            // Wait for half the window
            System.Threading.Thread.Sleep(windowSize / 2);

            // These requests should be denied as we're still within the window
            Assert.That(RateLimitingHelper.IsAllowed(key, windowSize, maxRequests), Is.False,
                "Request should be denied during mid-window");
            Assert.That(RateLimitingHelper.IsAllowed(key, windowSize, maxRequests), Is.False,
                "Request should be denied during mid-window");
            Assert.That(RateLimitingHelper.IsAllowed(key, windowSize, maxRequests), Is.False,
                "Request should be denied during mid-window");

            // Wait for the remaining window
            System.Threading.Thread.Sleep(windowSize / 2 + 50);

            // Should allow 2 requests as older ones have expired
            Assert.That(RateLimitingHelper.IsAllowed(key, windowSize, maxRequests), Is.True,
                "First request after partial window expiry should be allowed");
            Assert.That(RateLimitingHelper.IsAllowed(key, windowSize, maxRequests), Is.True,
                "Second request after partial window expiry should be allowed");
            Assert.That(RateLimitingHelper.IsAllowed(key, windowSize, maxRequests), Is.False,
                "Third request should be denied");

            // Wait for full window
            System.Threading.Thread.Sleep(windowSize + 50);

            Assert.That(RateLimitingHelper.IsAllowed(key, windowSize, maxRequests), Is.True,
                "Request after full window expiry should be allowed");
        }

        [Test]
        public void ShouldHandleDifferentWindowSizes()
        {
            // Arrange
            string key = "test_key";
            int windowSize = 1000; // 1 second window
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
        public void ShouldHandleSlidingWindowEdgeCase()
        {
            // Arrange
            string key = "test_key";
            int windowSize = 1000;
            int maxRequests = 5;

            // Act & Assert
            for (int i = 0; i < maxRequests; i++)
            {
                Assert.That(RateLimitingHelper.IsAllowed(key, windowSize, maxRequests), Is.True,
                    $"Request {i + 1} should be allowed");
            }

            System.Threading.Thread.Sleep(windowSize + 50);

            Assert.That(RateLimitingHelper.IsAllowed(key, windowSize, maxRequests), Is.True,
                "Request after first window should be allowed");

            System.Threading.Thread.Sleep(windowSize + 50);

            Assert.That(RateLimitingHelper.IsAllowed(key, windowSize, maxRequests), Is.True,
                "Request after second window should be allowed");
        }

        [Test]
        public void IsRequestAllowed_ShouldRespectExactMatch()
        {
            // Arrange
            var context = new Context { Method = "GET", Route = "api/users" };
            context.User = new User("user123", "Test User");

            var endpoints = new List<EndpointConfig>
            {
                new EndpointConfig
                {
                    Method = "GET",
                    Route = "api/users",
                    RateLimiting = new RateLimitingConfig { Enabled = true, MaxRequests = 2, WindowSizeInMS = 1000 }
                }
            };

            // Act & Assert
            // First two requests should be allowed
            var (isAllowed1, _) = RateLimitingHelper.IsRequestAllowed(context, endpoints);
            var (isAllowed2, _) = RateLimitingHelper.IsRequestAllowed(context, endpoints);
            var (isAllowed3, effectiveConfig) = RateLimitingHelper.IsRequestAllowed(context, endpoints);

            Assert.That(isAllowed1, Is.True);
            Assert.That(isAllowed2, Is.True);
            Assert.That(isAllowed3, Is.False);
            Assert.That(effectiveConfig, Is.Not.Null);
            Assert.That(effectiveConfig.MaxRequests, Is.EqualTo(2));
        }

        [Test]
        public void IsRequestAllowed_ShouldRespectWildcardMatch()
        {
            // Arrange
            var context = new Context { Method = "GET", Route = "api/users", Url = "api/users" };
            context.User = new User("user123", "Test User");

            var endpoints = new List<EndpointConfig>
            {
                new EndpointConfig
                {
                    Method = "GET",
                    Route = "api/*",
                    RateLimiting = new RateLimitingConfig { Enabled = true, MaxRequests = 2, WindowSizeInMS = 1000 }
                }
            };

            // Act & Assert
            // First two requests should be allowed
            var (isAllowed1, _) = RateLimitingHelper.IsRequestAllowed(context, endpoints);
            var (isAllowed2, _) = RateLimitingHelper.IsRequestAllowed(context, endpoints);
            var (isAllowed3, effectiveConfig) = RateLimitingHelper.IsRequestAllowed(context, endpoints);

            Assert.That(isAllowed1, Is.True);
            Assert.That(isAllowed2, Is.True);
            Assert.That(isAllowed3, Is.False);
            Assert.That(effectiveConfig, Is.Not.Null);
            Assert.That(effectiveConfig.MaxRequests, Is.EqualTo(2));
        }

        [Test]
        public void IsRequestAllowed_ShouldRespectMostExactMatch()
        {
            // Arrange
            var context = new Context { Method = "GET", Route = "api/users" };
            context.User = new User("user123", "Test User");

            var endpoints = new List<EndpointConfig>
            {
                new EndpointConfig
                {
                    Method = "GET",
                    Route = "api/users",
                    RateLimiting = new RateLimitingConfig { Enabled = true, MaxRequests = 3, WindowSizeInMS = 1000 }
                },
                new EndpointConfig
                {
                    Method = "GET",
                    Route = "api/*",
                    RateLimiting = new RateLimitingConfig { Enabled = true, MaxRequests = 2, WindowSizeInMS = 1000 }
                }
            };

            // Act & Assert
            // First three requests should be allowed by both exact and wildcard
            var (isAllowed1, _) = RateLimitingHelper.IsRequestAllowed(context, endpoints);
            var (isAllowed2, _) = RateLimitingHelper.IsRequestAllowed(context, endpoints);
            var (isAllowed3, _) = RateLimitingHelper.IsRequestAllowed(context, endpoints);
            var (isAllowed4, effectiveConfig) = RateLimitingHelper.IsRequestAllowed(context, endpoints);

            Assert.That(isAllowed1, Is.True);
            Assert.That(isAllowed2, Is.True);
            Assert.That(isAllowed3, Is.True);
            Assert.That(isAllowed4, Is.False);
            Assert.That(effectiveConfig, Is.Not.Null);
            Assert.That(effectiveConfig.MaxRequests, Is.EqualTo(3));
        }

        [Test]
        public void IsRequestAllowed_ShouldHandleMultipleWildcardMatches()
        {
            // Arrange
            var context = new Context { Method = "GET", Route = "api/users/details" };
            context.User = new User("user123", "Test User");
            context.Url = "https://example.com/api/users/details";

            var endpoints = new List<EndpointConfig>
            {
                new EndpointConfig
                {
                    Method = "GET",
                    Route = "api/*",
                    RateLimiting = new RateLimitingConfig { Enabled = true, MaxRequests = 3, WindowSizeInMS = 1000 }
                },
                new EndpointConfig
                {
                    Method = "GET",
                    Route = "*/users/*",
                    RateLimiting = new RateLimitingConfig { Enabled = true, MaxRequests = 2, WindowSizeInMS = 1000 }
                }
            };

            // Act & Assert
            // First two requests should be allowed by both wildcards
            var (isAllowed1, _) = RateLimitingHelper.IsRequestAllowed(context, endpoints);
            var (isAllowed2, _) = RateLimitingHelper.IsRequestAllowed(context, endpoints);
            var (isAllowed3, effectiveConfig) = RateLimitingHelper.IsRequestAllowed(context, endpoints);

            Assert.That(isAllowed1, Is.True);
            Assert.That(isAllowed2, Is.True);
            Assert.That(isAllowed3, Is.False);
            Assert.That(effectiveConfig, Is.Not.Null);
            Assert.That(effectiveConfig.MaxRequests, Is.EqualTo(2));
        }

        [Test]
        public void IsRequestAllowed_ShouldHandleDisabledConfigs()
        {
            // Arrange
            var context = new Context { Method = "GET", Route = "api/users" };
            context.User = new User("user123", "Test User");

            var endpoints = new List<EndpointConfig>
            {
                new EndpointConfig
                {
                    Method = "GET",
                    Route = "api/users",
                    RateLimiting = new RateLimitingConfig { Enabled = false, MaxRequests = 1, WindowSizeInMS = 1000 }
                },
                new EndpointConfig
                {
                    Method = "GET",
                    Route = "api/*",
                    RateLimiting = new RateLimitingConfig { Enabled = false, MaxRequests = 1, WindowSizeInMS = 1000 }
                }
            };

            // Act & Assert
            // All requests should be allowed because both configs are disabled
            for (int i = 0; i < 5; i++)
            {
                var (isAllowed, _) = RateLimitingHelper.IsRequestAllowed(context, endpoints);
                Assert.That(isAllowed, Is.True);
            }
        }

        [Test]
        public void IsRequestAllowed_ShouldHandleEmptyEndpoints()
        {
            // Arrange
            var context = new Context { Method = "GET", Route = "api/users" };
            context.User = new User("user123", "Test User");

            var endpoints = new List<EndpointConfig>();

            // Act
            var (isAllowed, effectiveConfig) = RateLimitingHelper.IsRequestAllowed(context, endpoints);

            // Assert
            Assert.That(isAllowed, Is.True);
            Assert.That(effectiveConfig, Is.Null);
        }

        [Test]
        public void IsRequestAllowed_ShouldHandleNullEndpoints()
        {
            // Arrange
            var context = new Context { Method = "GET", Route = "api/users" };
            context.User = new User("user123", "Test User");

            // Act
            var (isAllowed, effectiveConfig) = RateLimitingHelper.IsRequestAllowed(context, null);

            // Assert
            Assert.That(isAllowed, Is.True);
            Assert.That(effectiveConfig, Is.Null);
        }
    }
}
