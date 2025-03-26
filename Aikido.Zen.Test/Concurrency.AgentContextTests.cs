using System.Text.RegularExpressions;
using System.Threading;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Models;

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
                            Method = "GET"
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
        public void ConcurrentAccess_ToRateLimitedRoutes_ShouldBeThreadSafe()
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
                        _agentContext.AddRateLimitedEndpoint(
                            $"GET|/api/test{threadId}-{j}",
                            new RateLimitingConfig { MaxRequests = 60, WindowSizeInMS = 1000 }
                        );
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
            Assert.That(_agentContext.RateLimitedRoutes.Count, Is.EqualTo(numThreads * numRoutesPerThread));
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
                            Method = "GET"
                        });

                        // Add rate limited endpoint
                        _agentContext.AddRateLimitedEndpoint(
                            $"GET|/api/test{threadId}-{j}",
                            new RateLimitingConfig { MaxRequests = 60 }
                        );
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
                Assert.That(_agentContext.RateLimitedRoutes.Count, Is.EqualTo(numThreads * numOperationsPerThread));
            });
        }
    }
}
