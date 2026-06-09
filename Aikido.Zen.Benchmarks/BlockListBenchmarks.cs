using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Aikido.Zen.Core.Models.Ip;
using System.Collections.Generic;
using BenchmarkDotNet.Columns;
using System.Linq;
using Aikido.Zen.Core;

namespace Aikido.Zen.Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net10_0, baseline: true, warmupCount: 3, iterationCount: 15, invocationCount: 1)]
    [MinIterationTime(100)]
    [Outliers(Perfolizer.Mathematics.OutlierDetection.OutlierMode.RemoveAll)]
    [HideColumns(Column.StdErr, Column.StdDev, Column.Error, Column.Min, Column.Max, Column.RatioSD)]
    public class BlockListBenchmarks
    {
        private const int ChecksPerInvocation = 20_000;

        private BlockList _blockList;
        private List<string> _ipRanges;
        private List<string> _checkIps;
        private List<Context> _checkContexts;

        [Params(10_000)] // Number of IP ranges to block
        public int BlockedIpRangeCount { get; set; }

        [Params(1, 100, 1000)] // Number of IPs to check
        public int IpsToCheck { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _blockList = new BlockList();
            _ipRanges = new List<string>(BlockedIpRangeCount);
            _checkIps = new List<string>();
            _checkContexts = new List<Context>();

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

            foreach (var ip in _checkIps)
            {
                _checkContexts.Add(new Context
                {
                    Method = "GET",
                    Route = "/path",
                    RemoteAddress = ip,
                    Url = "http://localhost:80/path"
                });
            }

            // Update blocked subnets
            _blockList.UpdateBlockedIps(new[] { ("benchmark", _ipRanges.AsEnumerable()) });
        }

        [Benchmark]
        public void CheckBlockedIPs()
        {
            var repetitions = RepetitionsForIpsToCheck();
            for (int repetition = 0; repetition < repetitions; repetition++)
            {
                // Check if IPs are blocked
                for (int i = 0; i < IpsToCheck; i++)
                {
                    _blockList.IsIPBlocked(_checkIps[i]);
                }
            }
        }

        [Benchmark]
        public void CheckIsBlocked()
        {
            var repetitions = RepetitionsForIpsToCheck();
            for (int repetition = 0; repetition < repetitions; repetition++)
            {
                // Check if access is blocked based on IP and path
                for (int i = 0; i < IpsToCheck; i++)
                {
                    _blockList.IsBlocked(_checkContexts[i], out var reason);
                }
            }
        }

        private int RepetitionsForIpsToCheck()
        {
            return (ChecksPerInvocation + IpsToCheck - 1) / IpsToCheck;
        }
    }
}
