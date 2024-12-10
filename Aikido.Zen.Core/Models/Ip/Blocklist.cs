using System;
using System.Collections.Generic;
using System.Net;
using Aikido.Zen.Core.Helpers;
using NetTools;

namespace Aikido.Zen.Core.Models.Ip
{
    public class BlockList
    {
        private readonly HashSet<string> _blockedAddresses = new HashSet<string>();
        private readonly List<IPAddressRange> _blockedSubnets = new List<IPAddressRange>();

        public BlockList()
        {
            _blockedAddresses = new HashSet<string>();
            _blockedSubnets = new List<IPAddressRange>();
        }

        public void UpdateBlockedSubnets(IEnumerable<IPAddressRange> subnets)
        {
            _blockedSubnets.Clear();
            _blockedSubnets.AddRange(subnets);
        }

        public void AddIpAddressToBlocklist(string ip)
        {
            if (!_blockedAddresses.Contains(ip))
            {
                _blockedAddresses.Add(ip);
            }
        }

        public bool IsBlocked(string ip)
        {
            if (_blockedAddresses.Contains(ip))
            {
                return true;
            }

            if (IPAddress.TryParse(ip, out var address))
            {
                foreach (var subnet in _blockedSubnets)
                {
                    if (IPHelper.IsInSubnet(address, subnet))
                    {
                        AddIpAddressToBlocklist(ip);
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
