using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Models.Ip;
using System.Collections.Generic;
using BenchmarkDotNet.Columns;
using System.Linq;
using Aikido.Zen.Core;
using System.Threading.Tasks;

namespace Aikido.Zen.Benchmarks
{
    /// <summary>
    /// Benchmarks for testing the performance of AgentContext operations, particularly focusing on thread-safety
    /// and concurrent access patterns.
    /// </summary>
    [SimpleJob(RuntimeMoniker.Net48, baseline: false, warmupCount: 1, iterationCount: 2)]
    [SimpleJob(RuntimeMoniker.Net80, baseline: true, warmupCount: 1, iterationCount: 2)]
    [HideColumns(Column.StdErr, Column.StdDev, Column.Error, Column.Min, Column.Max, Column.RatioSD)]
    public class AgentContextBenchmarks
    {
        private AgentContext _agentContext;
        private List<Context> _testContexts;
        private List<User> _testUsers;
        private List<string> _testHostnames;
        private List<EndpointConfig> _testEndpoints;

        [Params(1000)] // Number of test items to generate
        public int TestItemCount { get; set; }

        [Params(1, 10, 100)] // Number of concurrent operations
        public int ConcurrentOperations { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _agentContext = new AgentContext();
            _testContexts = new List<Context>(TestItemCount);
            _testUsers = new List<User>(TestItemCount);
            _testHostnames = new List<string>(TestItemCount);
            _testEndpoints = new List<EndpointConfig>(TestItemCount);

            // Initialize test data
            for (int i = 0; i < TestItemCount; i++)
            {
                _testContexts.Add(new Context
                {
                    Method = "GET",
                    Route = $"/api/test/{i}",
                    RemoteAddress = $"192.168.1.{i}",
                    Url = $"http://localhost:80/api/test/{i}",
                    UserAgent = $"TestUserAgent_{i}"
                });

                _testUsers.Add(new User($"user_{i}", $"Test User {i}"));
                _testHostnames.Add($"test{i}.example.com:80");
                _testEndpoints.Add(new EndpointConfig
                {
                    Method = "GET",
                    Route = $"/api/test/{i}",
                    RateLimiting = new RateLimitingConfig { MaxRequests = 100 }
                });
            }

            // Initialize some blocked users and IPs
            var blockedUsers = Enumerable.Range(0, TestItemCount / 10)
                .Select(i => $"user_{i}")
                .ToList();
            _agentContext.UpdateBlockedUsers(blockedUsers);

            var blockedIps = Enumerable.Range(0, TestItemCount / 10)
                .Select(i => $"192.168.1.{i}/32")
                .ToList();
            _agentContext.UpdateFirewallLists(new Core.Api.FirewallListsAPIResponse
            {
                BlockedIPAddresses = new List<Core.Api.FirewallListsAPIResponse.IPList> {
                new Core.Api.FirewallListsAPIResponse.IPList {
                    Ips = blockedIps
                }
                }
            });
        }

        [Benchmark]
        public void AddRequestsConcurrent()
        {
            Parallel.For(0, ConcurrentOperations, i =>
            {
                _agentContext.AddRequest();
            });
        }

        [Benchmark]
        public void AddUsersConcurrent()
        {
            Parallel.For(0, ConcurrentOperations, i =>
            {
                var user = _testUsers[i % TestItemCount];
                _agentContext.AddUser(user, $"192.168.1.{i}");
            });
        }

        [Benchmark]
        public void AddRoutesConcurrent()
        {
            Parallel.For(0, ConcurrentOperations, i =>
            {
                var context = _testContexts[i % TestItemCount];
                _agentContext.AddRoute(context);
            });
        }

        [Benchmark]
        public void AddHostnamesConcurrent()
        {
            Parallel.For(0, ConcurrentOperations, i =>
            {
                var hostname = _testHostnames[i % TestItemCount];
                _agentContext.AddHostname(hostname);
            });
        }

        [Benchmark]
        public void IsBlockedCheckConcurrent()
        {
            Parallel.For(0, ConcurrentOperations, i =>
            {
                var context = _testContexts[i % TestItemCount];
                _agentContext.IsBlocked(context, out var reason);
            });
        }

        [Benchmark]
        public void UpdateConfigConcurrent()
        {
            var response = new Core.Api.ReportingAPIResponse
            {
                Block = true,
                BlockedUserIds = _testUsers.Select(u => u.Id).ToList(),
                Endpoints = _testEndpoints,
                ConfigUpdatedAt = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            Parallel.For(0, ConcurrentOperations, i =>
            {
                _agentContext.UpdateConfig(response);
            });
        }

        [Benchmark]
        public void UpdateFirewallListsConcurrent()
        {
            var blockedIps = _testContexts.Select(c => c.RemoteAddress + "/32").ToList();
            var response = new Core.Api.FirewallListsAPIResponse
            {
                BlockedIPAddresses = new List<Core.Api.FirewallListsAPIResponse.IPList> {
                    new Core.Api.FirewallListsAPIResponse.IPList {
                        Ips = blockedIps
                    }
                },
                BlockedUserAgents = "bots|bots|bots"
            };

            Parallel.For(0, ConcurrentOperations, i =>
            {
                _agentContext.UpdateFirewallLists(response);
            });
        }
    }
}
