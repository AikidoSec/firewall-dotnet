using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Aikido.Zen.Core.Models.Ip;
using System.Collections.Generic;
using BenchmarkDotNet.Columns;

namespace Aikido.Zen.Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net48, baseline: false, warmupCount: 1, iterationCount: 2)]
    [SimpleJob(RuntimeMoniker.Net80, baseline: true, warmupCount: 1, iterationCount: 2)]
    [HideColumns(Column.StdErr, Column.StdDev, Column.Error, Column.Min, Column.Max, Column.RatioSD)]
    public class BlockListBenchmarks
    {
        private BlockList _blockList;
        private List<string> _ipRanges;
        private List<string> _userIds;
        private List<string> _checkIps;
        private List<string> _checkUserIds;

        [Params(100000)] // Number of IP ranges to block
        public int BlockedIpRangeCount { get; set; }

        [Params(1000)] // Number of users to test
        public int UserCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _blockList = new BlockList();
            _ipRanges = new List<string>(BlockedIpRangeCount);
            _userIds = new List<string>(UserCount);
            _checkIps = new List<string>(UserCount);
            _checkUserIds = new List<string>(UserCount);

            // Initialize test data
            for (int i = 0; i < BlockedIpRangeCount; i++)
            {
                _ipRanges.Add($"192.168.{i / 256}.{i % 256}/32");
            }

            for (int i = 0; i < UserCount; i++)
            {
                _userIds.Add($"user-{i}");
                _checkIps.Add(i < UserCount / 2 ? $"192.168.{i / 256}.{i % 256}" : $"10.0.{i / 256}.{i % 256}");
                _checkUserIds.Add($"user-{i}");
            }

            // Update blocked subnets
            _blockList.UpdateBlockedSubnets(_ipRanges);
        }

        [Benchmark]
        public void CheckBlockedIPs()
        {
            // Check if IPs are blocked
            foreach (var ip in _checkIps)
            {
                _blockList.IsIPBlocked(ip);
            }
        }

        [Benchmark]
        public void CheckIsBlocked()
        {
            // Check if access is blocked based on IP and userId
            for (int i = 0; i < _checkIps.Count; i++)
            {
                _blockList.IsBlocked(_checkIps[i], $"GET|user/{_checkUserIds[i]}");
            }
        }
    }
}


