using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Jobs;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Api;
using Aikido.Zen.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aikido.Zen.Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net10_0, baseline: true)]
    [Outliers(Perfolizer.Mathematics.OutlierDetection.OutlierMode.RemoveAll)]
    [HideColumns(Column.StdErr, Column.StdDev, Column.Error, Column.Min, Column.Max, Column.RatioSD)]
    public class AgentContextBenchmarks
    {
        private Context[] _contexts;
        private User[] _users;
        private string[] _hostnames;
        private EndpointConfig[] _endpoints;
        private ReportingAPIResponse _configResponse;
        private FirewallListsAPIResponse _firewallListsResponse;

        [Params(1000)]
        public int ItemCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _contexts = new Context[ItemCount];
            _users = new User[ItemCount];
            _hostnames = new string[ItemCount];
            _endpoints = new EndpointConfig[ItemCount];

            for (var i = 0; i < ItemCount; i++)
            {
                _contexts[i] = new Context
                {
                    Method = "GET",
                    Route = $"/api/test/{i}",
                    RemoteAddress = $"192.168.{i / 256}.{i % 256}",
                    Url = $"http://localhost:80/api/test/{i}",
                    UserAgent = $"TestUserAgent_{i}"
                };

                _users[i] = new User($"user_{i}", $"Test User {i}");
                _hostnames[i] = $"test{i}.example.com:80";
                _endpoints[i] = new EndpointConfig
                {
                    Method = "GET",
                    Route = $"/api/test/{i}",
                    RateLimiting = new RateLimitingConfig
                    {
                        Enabled = true,
                        MaxRequests = 100,
                        WindowSizeInMS = 60000
                    }
                };
            }

            _configResponse = new ReportingAPIResponse
            {
                Block = true,
                BlockedUserIds = _users.Take(ItemCount / 10).Select(user => user.Id).ToArray(),
                Endpoints = _endpoints,
                ConfigUpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            _firewallListsResponse = new FirewallListsAPIResponse
            {
                BlockedIPAddresses = new[]
                {
                    new FirewallListsAPIResponse.IPList
                    {
                        Key = "benchmark-blocked-ips",
                        Ips = _contexts.Take(ItemCount / 10).Select(context => context.RemoteAddress + "/32").ToArray()
                    }
                },
                BlockedUserAgents = "TestBot|BadBot"
            };
        }

        [Benchmark]
        public int TrackRequestMetadata()
        {
            var agentContext = new AgentContext();

            for (var i = 0; i < ItemCount; i++)
            {
                agentContext.AddRequest();
                agentContext.AddHostname(_hostnames[i]);
                agentContext.AddUser(_users[i], _contexts[i].RemoteAddress);
                agentContext.AddRoute(_contexts[i]);
            }

            return agentContext.Requests +
                agentContext.Hostnames.Count() +
                agentContext.Users.Count() +
                agentContext.Routes.Count();
        }

        [Benchmark]
        public int ApplyConfiguration()
        {
            var agentContext = new AgentContext();

            agentContext.Config.UpdateConfig(_configResponse);
            agentContext.Config.UpdateFirewallLists(_firewallListsResponse);

            return agentContext.Config.Endpoints.Count();
        }

        [Benchmark]
        public int CheckBlockedRequests()
        {
            var agentContext = new AgentContext();
            agentContext.Config.UpdateConfig(_configResponse);
            agentContext.Config.UpdateFirewallLists(_firewallListsResponse);

            var blocked = 0;
            for (var i = 0; i < ItemCount; i++)
            {
                if (agentContext.IsBlocked(_contexts[i], out _))
                {
                    blocked++;
                }
            }

            return blocked;
        }
    }
}
