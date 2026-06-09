using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Aikido.Zen.Core.Models.Ip;
using System.Collections.Generic;
using BenchmarkDotNet.Columns;
using System.Linq;
using Aikido.Zen.Core;

namespace Aikido.Zen.Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net10_0, baseline: true)]
    [MinIterationTime(100)]
    [Outliers(Perfolizer.Mathematics.OutlierDetection.OutlierMode.RemoveAll)]
    [HideColumns(Column.StdErr, Column.StdDev, Column.Error, Column.Min, Column.Max, Column.RatioSD)]
    public class BlockListBenchmarks
    {
        private BlockList _blockList;
        private List<string> _ipRanges;
        private string _blockedIp;
        private Context _blockedContext;

        [Params(10_000)] // Number of IP ranges to block
        public int BlockedIpRangeCount { get; set; }

        [Params("IPv4", "IPv6")]
        public string AddressFamily { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _blockList = new BlockList();
            _ipRanges = new List<string>(BlockedIpRangeCount);

            // Initialize test data
            for (int i = 0; i < BlockedIpRangeCount; i++)
            {
                _ipRanges.Add($"192.168.{i / 256}.{i % 256}/32");
                _ipRanges.Add($"10.{i / 256}.{i % 256}.0/24");
                // ipv6
                _ipRanges.Add($"2001:{i:X4}:{i:X4}:{i:X4}:{i:X4}:{i:X4}:{i:X4}:{i:X4}/128");
            }

            // Update blocked subnets
            _blockList.UpdateBlockedIps(new[] { ("benchmark", _ipRanges.AsEnumerable()) });

            _blockedIp = AddressFamily == "IPv6"
                ? "2001:0000:0000:0000:0000:0000:0000:0000"
                : "10.0.0.1";
            _blockedContext = new Context
            {
                Method = "GET",
                Route = "/path",
                RemoteAddress = _blockedIp,
                Url = "http://localhost:80/path"
            };
        }

        [Benchmark]
        public bool CheckBlockedIP()
        {
            return _blockList.IsIPBlocked(_blockedIp);
        }

        [Benchmark]
        public bool CheckIsBlocked()
        {
            return _blockList.IsBlocked(_blockedContext, out var reason);
        }
    }
}
