using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Aikido.Zen.Core.Models;
using Aikido.Zen.Core.Models.Ip;
using System.Collections.Generic;
using BenchmarkDotNet.Columns;
using System.Linq;
using Aikido.Zen.Core;
using System.Threading.Tasks;
using System;

namespace Aikido.Zen.Benchmarks
{
    /// <summary>
    /// Benchmarks for testing the performance of AgentContext operations, particularly focusing on thread-safety
    /// and concurrent access patterns.
    /// </summary>
    [SimpleJob(RuntimeMoniker.Net10_0, baseline: true, warmupCount: 3, iterationCount: 15, invocationCount: 1)]
    [MinIterationTime(100)]
    [Outliers(Perfolizer.Mathematics.OutlierDetection.OutlierMode.RemoveAll)]
    [HideColumns(Column.StdErr, Column.StdDev, Column.Error, Column.Min, Column.Max, Column.RatioSD)]
    public class AgentContextBenchmarks
    {
        private const int FastOperationsPerInvocation = 20_000_000;
        private const int ContextOperationsPerInvocation = 100_000;
        private const int ConfigOperationsPerInvocation = 500;

        private AgentContext _agentContext;
        private List<Context> _testContexts;
        private List<User> _testUsers;
        private List<string> _testHostnames;
        private List<EndpointConfig> _testEndpoints;
        private List<string> _testIpAddresses;
        private Core.Api.ReportingAPIResponse _configResponse;
        private Core.Api.FirewallListsAPIResponse _firewallListsResponse;

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
            _testIpAddresses = new List<string>(TestItemCount);

            // Initialize test data
            for (int i = 0; i < TestItemCount; i++)
            {
                var ipAddress = $"192.168.1.{i}";
                _testContexts.Add(new Context
                {
                    Method = "GET",
                    Route = $"/api/test/{i}",
                    RemoteAddress = ipAddress,
                    Url = $"http://localhost:80/api/test/{i}",
                    UserAgent = $"TestUserAgent_{i}"
                });

                _testUsers.Add(new User($"user_{i}", $"Test User {i}"));
                _testHostnames.Add($"test{i}.example.com:80");
                _testIpAddresses.Add(ipAddress);
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

            _configResponse = new Core.Api.ReportingAPIResponse
            {
                Block = true,
                BlockedUserIds = _testUsers.Select(u => u.Id).ToList(),
                Endpoints = _testEndpoints,
                ConfigUpdatedAt = 1
            };

            var firewallBlockedIps = _testContexts.Select(c => c.RemoteAddress + "/32").ToList();
            _firewallListsResponse = new Core.Api.FirewallListsAPIResponse
            {
                BlockedIPAddresses = new List<Core.Api.FirewallListsAPIResponse.IPList> {
                    new Core.Api.FirewallListsAPIResponse.IPList {
                        Ips = firewallBlockedIps
                    }
                },
                BlockedUserAgents = "bots|bots|bots"
            };
        }

        [Benchmark]
        public void AddRequestsConcurrent()
        {
            RunParallel(FastOperationsPerInvocation, i =>
            {
                _agentContext.AddRequest();
            });
        }

        [Benchmark]
        public void AddUsersConcurrent()
        {
            RunParallel(ContextOperationsPerInvocation, i =>
            {
                var user = _testUsers[i % TestItemCount];
                _agentContext.AddUser(user, _testIpAddresses[i % TestItemCount]);
            });
        }

        [Benchmark]
        public void AddRoutesConcurrent()
        {
            RunParallel(ContextOperationsPerInvocation, i =>
            {
                var context = _testContexts[i % TestItemCount];
                _agentContext.AddRoute(context);
            });
        }

        [Benchmark]
        public void AddHostnamesConcurrent()
        {
            RunParallel(ContextOperationsPerInvocation, i =>
            {
                var hostname = _testHostnames[i % TestItemCount];
                _agentContext.AddHostname(hostname);
            });
        }

        [Benchmark]
        public void IsBlockedCheckConcurrent()
        {
            RunParallel(ContextOperationsPerInvocation, i =>
            {
                var context = _testContexts[i % TestItemCount];
                _agentContext.IsBlocked(context, out var reason);
            });
        }

        [Benchmark]
        public void UpdateConfigConcurrent()
        {
            RunParallel(ConfigOperationsPerInvocation, i =>
            {
                _agentContext.UpdateConfig(_configResponse);
            });
        }

        [Benchmark]
        public void UpdateFirewallListsConcurrent()
        {
            RunParallel(ConfigOperationsPerInvocation, i =>
            {
                _agentContext.UpdateFirewallLists(_firewallListsResponse);
            });
        }

        private void RunParallel(int operationCount, Action<int> action)
        {
            var workerCount = Math.Min(ConcurrentOperations, operationCount);
            Parallel.For(
                0,
                workerCount,
                new ParallelOptions { MaxDegreeOfParallelism = workerCount },
                worker =>
                {
                    for (var i = worker; i < operationCount; i += workerCount)
                    {
                        action(i);
                    }
                });
        }
    }
}
