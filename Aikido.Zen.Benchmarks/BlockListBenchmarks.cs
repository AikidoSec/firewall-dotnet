using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Aikido.Zen.Core.Models.Ip;
using System.Collections.Generic;
using BenchmarkDotNet.Columns;
using System.Linq;

namespace Aikido.Zen.Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net48, baseline: false, warmupCount: 1, iterationCount: 2)]
    [SimpleJob(RuntimeMoniker.Net80, baseline: true, warmupCount: 1, iterationCount: 2)]
    [HideColumns(Column.StdErr, Column.StdDev, Column.Error, Column.Min, Column.Max, Column.RatioSD)]
    public class BlockListBenchmarks
    {
        private BlockList _blockList;
        private List<string> _ipRanges;
        private List<string> _checkIps;

        [Params(100000)] // Number of IP ranges to block
        public int BlockedIpRangeCount { get; set; }

        [Params(1, 100, 1000)] // Number of IPs to check
        public int IpsToCheck { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _blockList = new BlockList();
            _ipRanges = new List<string>(BlockedIpRangeCount);
            _checkIps = new List<string>();

            // Initialize test data
            for (int i = 0; i < BlockedIpRangeCount; i++)
            {
                _ipRanges.Add($"192.168.{i / 256}.{i % 256}/32");
                _ipRanges.Add($"10.{i / 256}.{i % 256}.0/24");
                // ipv6
                _ipRanges.Add($"2001:{i:X4}:{i:X4}:{i:X4}:{i:X4}:{i:X4}:{i:X4}:{i:X4}/128");
            }

            for (int i = 0; i < BlockedIpRangeCount; i++)
            {
                if (i < BlockedIpRangeCount / 2)
                    _checkIps.Add($"10.{i / 256}.{i % 256}.0/24");
                // ipv6
                if (i < BlockedIpRangeCount / 2)
                    _checkIps.Add($"2001:{i:X4}:{i:X4}:{i:X4}:{i:X4}:{i:X4}:{i:X4}:{i:X4}");
            }

            // Update blocked subnets
            _blockList.UpdateBlockedIps(_ipRanges);
        }

        [Benchmark]
        public void CheckBlockedIPs()
        {
            // Check if IPs are blocked
            foreach (var ip in _checkIps.Take(IpsToCheck))
            {
                _blockList.IsIPBlocked(ip);
            }
        }

        [Benchmark]
        public void CheckIsBlocked()
        {
            // Check if access is blocked based on IP and path
            foreach (var ip in _checkIps.Take(IpsToCheck))
            {
                _blockList.IsBlocked(ip, $"GET|path/", out var reason);
            }
        }
    }
}
