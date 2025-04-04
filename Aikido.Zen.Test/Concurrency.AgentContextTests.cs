using System.Text.RegularExpressions;
using System.Threading;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Models;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;

namespace Aikido.Zen.Test
{
    public class ConcurrencyAgentContextTests
    {
        private AgentContext _agentContext;

        [SetUp]
        public void Setup()
        {
            _agentContext = new AgentContext();
        }

        [Test]
        public void ConcurrentAccess_ToHostnames_ShouldBeThreadSafe()
        {
            // Arrange
            const int numThreads = 10;
            const int numHostnamesPerThread = 100;
            var threads = new List<Thread>();

            // Act
            for (int i = 0; i < numThreads; i++)
            {
                var threadId = i;
                var thread = new Thread(() =>
                {
                    for (int j = 0; j < numHostnamesPerThread; j++)
                    {
                        _agentContext.AddHostname($"host{threadId}-{j}.com:8080");
                    }
                });
                threads.Add(thread);
                thread.Start();
            }

            // Wait for all threads to complete
            foreach (var thread in threads)
            {
                thread.Join();
            }

            // Assert
            Assert.That(_agentContext.Hostnames.Count(), Is.EqualTo(numThreads * numHostnamesPerThread));
        }

        [Test]
        public void ConcurrentAccess_ToUsers_ShouldBeThreadSafe()
        {
            // Arrange
            const int numThreads = 10;
            const int numUsersPerThread = 100;
            var threads = new List<Thread>();

            // Act
            for (int i = 0; i < numThreads; i++)
            {
                var threadId = i;
                var thread = new Thread(() =>
                {
                    for (int j = 0; j < numUsersPerThread; j++)
                    {
                        _agentContext.AddUser(new User($"user{threadId}-{j}", $"User {threadId}-{j}"), $"192.168.{threadId}.{j}");
                    }
                });
                threads.Add(thread);
                thread.Start();
            }

            // Wait for all threads to complete
            foreach (var thread in threads)
            {
                thread.Join();
            }

            // Assert
            Assert.That(_agentContext.Users.Count(), Is.EqualTo(numThreads * numUsersPerThread));
        }

        [Test]
        public void ConcurrentAccess_ToRoutes_ShouldBeThreadSafe()
        {
            // Arrange
            const int numThreads = 10;
            const int numRoutesPerThread = 100;
            var threads = new List<Thread>();

            // Act
            for (int i = 0; i < numThreads; i++)
            {
                var threadId = i;
                var thread = new Thread(() =>
                {
                    for (int j = 0; j < numRoutesPerThread; j++)
                    {
                        _agentContext.AddRoute(new Context
                        {
                            Url = $"/api/test{threadId}-{j}",
                            Method = "GET",
                            Route = $"/api/test{threadId}-{j}"
                        });
                    }
                });
                threads.Add(thread);
                thread.Start();
            }

            // Wait for all threads to complete
            foreach (var thread in threads)
            {
                thread.Join();
            }

            // Assert
            Assert.That(_agentContext.Routes.Count(), Is.EqualTo(numThreads * numRoutesPerThread));
        }

        [Test]
        public void ConcurrentAccess_ToEndpoints_ShouldBeThreadSafe()
        {
            // Arrange
            const int numThreads = 10;
            const int numEndpointsPerThread = 100;
            var threads = new List<Thread>();
            var allEndpoints = new ConcurrentDictionary<string, EndpointConfig>();

            // Act
            for (int i = 0; i < numThreads; i++)
            {
                var threadId = i;
                var thread = new Thread(() =>
                {
                    for (int j = 0; j < numEndpointsPerThread; j++)
                    {
                        var config = new EndpointConfig
                        {
                            Method = "GET",
                            Route = $"/api/test{threadId}-{j}",
                            RateLimiting = new RateLimitingConfig
                            {
                                Enabled = true,
                                MaxRequests = 60,
                                WindowSizeInMS = 1000
                            }
                        };

                        // Track this endpoint to verify it was stored correctly
                        string key = $"{threadId}-{j}";
                        allEndpoints.TryAdd(key, config);

                        var endpoints = new List<EndpointConfig> { config };
                        _agentContext.UpdateRatelimitedRoutes(endpoints);
                    }
                });
                threads.Add(thread);
                thread.Start();
            }

            // Wait for all threads to complete
            foreach (var thread in threads)
            {
                thread.Join();
            }

            // Assert - we should have at least one endpoint from each thread
            // Due to the nature of parallel updates, we can't guarantee exactly which ones will be there
            // But we can verify that the collection is consistent and doesn't crash
            var finalEndpoints = _agentContext.Endpoints.ToList();
            Assert.That(finalEndpoints, Is.Not.Null);
            Assert.That(finalEndpoints.Count, Is.GreaterThan(0));
            Assert.That(finalEndpoints.Count, Is.LessThanOrEqualTo(numThreads * numEndpointsPerThread));

            // Ensure no exception when accessing endpoints collection
            var methods = finalEndpoints.Select(e => e.Method).ToList();
            var routes = finalEndpoints.Select(e => e.Route).ToList();
            Assert.That(methods.Count, Is.EqualTo(finalEndpoints.Count));
            Assert.That(routes.Count, Is.EqualTo(finalEndpoints.Count));
        }

        [Test]
        public void ConcurrentAccess_ToMixedOperations_ShouldBeThreadSafe()
        {
            // Arrange
            const int numThreads = 10;
            const int numOperationsPerThread = 100;
            var threads = new List<Thread>();

            // Act
            for (int i = 0; i < numThreads; i++)
            {
                var threadId = i;
                var thread = new Thread(() =>
                {
                    for (int j = 0; j < numOperationsPerThread; j++)
                    {
                        // Add hostname
                        _agentContext.AddHostname($"host{threadId}-{j}.com:8080");

                        // Add user
                        _agentContext.AddUser(new User($"user{threadId}-{j}", $"User {threadId}-{j}"), $"192.168.{threadId}.{j}");

                        // Add route
                        _agentContext.AddRoute(new Context
                        {
                            Url = $"/api/test{threadId}-{j}",
                            Method = "GET",
                            Route = $"/api/test{threadId}-{j}"
                        });

                        // Update endpoints (last one overwrites previous ones from the same thread)
                        var endpoints = new List<EndpointConfig>
                        {
                            new EndpointConfig
                            {
                                Method = "GET",
                                Route = $"/api/test{threadId}-{j}",
                                RateLimiting = new RateLimitingConfig
                                {
                                    Enabled = true,
                                    MaxRequests = 60,
                                    WindowSizeInMS = 1000
                                }
                            }
                        };
                        _agentContext.UpdateRatelimitedRoutes(endpoints);
                    }
                });
                threads.Add(thread);
                thread.Start();
            }

            // Wait for all threads to complete
            foreach (var thread in threads)
            {
                thread.Join();
            }

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(_agentContext.Hostnames.Count(), Is.EqualTo(numThreads * numOperationsPerThread));
                Assert.That(_agentContext.Users.Count(), Is.EqualTo(numThreads * numOperationsPerThread));
                Assert.That(_agentContext.Routes.Count(), Is.EqualTo(numThreads * numOperationsPerThread));

                // Since many threads are updating the endpoints list, we can't guarantee exactly how many will be there,
                // but we can verify it's a consistent state
                var finalEndpoints = _agentContext.Endpoints.ToList();
                Assert.That(finalEndpoints, Is.Not.Null);
                Assert.That(finalEndpoints.Count, Is.GreaterThan(0));
                Assert.That(finalEndpoints.Count, Is.LessThanOrEqualTo(numThreads * numOperationsPerThread));
            });
        }
    }
}
