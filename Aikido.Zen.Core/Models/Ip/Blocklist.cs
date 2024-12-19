using System.Collections.Generic;
using System.Net;
using Aikido.Zen.Core.Helpers;
using NetTools;

namespace Aikido.Zen.Core.Models.Ip
{
    /// <summary>
    /// Manages IP blocklists and allowed subnet rules
    /// </summary>
    public class BlockList
    {
        private readonly HashSet<string> _blockedAddresses = new HashSet<string>();
        private readonly List<IPAddressRange> _blockedSubnets = new List<IPAddressRange>();
        private readonly IDictionary<string, IEnumerable<IPAddressRange>> _allowedSubnets = new Dictionary<string, IEnumerable<IPAddressRange>>();

        /// <summary>
        /// Updates the allowed subnet ranges per URL
        /// <param name="subnets">The subnet ranges</param>
        /// </summary>
        public void UpdateAllowedSubnets(IDictionary<string, IEnumerable<IPAddressRange>> subnets)
        {
            _allowedSubnets.Clear();
            foreach (var subnet in subnets)
            {
                _allowedSubnets.Add(subnet.Key, subnet.Value);
            }
        }

        /// <summary>
        /// Updates the blocked subnet ranges
        /// <param name="subnets">The subnet ranges</param>
        /// </summary>
        public void UpdateBlockedSubnets(IEnumerable<IPAddressRange> subnets)
        {
            _blockedSubnets.Clear();
            _blockedSubnets.AddRange(subnets);
        }

        /// <summary>
        /// Adds an IP address to the blocklist
        /// <param name="ip">The IP address</param>
        /// </summary>
        public void AddIpAddressToBlocklist(string ip)
        {
            if (!_blockedAddresses.Contains(ip))
            {
                _blockedAddresses.Add(ip);
            }
        }

        /// <summary>
        /// Checks if an IP address is blocked
        /// <param name="ip">The IP address</param>
        /// <returns>True if the IP is blocked, false otherwise</returns>
        /// </summary>
        public bool IsIPBlocked(string ip)
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
                        // we add this IP to the blocklist to avoid having to check it again
                        // this is done for performance reasons
                        AddIpAddressToBlocklist(ip);
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if an IP is allowed for a specific URL
        /// <param name="ip">The IP address</param>
        /// <param name="endpoint">The endpoint e.g. GET|the/path</param>
        /// </summary>
        public bool IsIPAllowed(string ip, string endpoint) {
            if (!_allowedSubnets.TryGetValue(endpoint, out var subnets)) {
                return true;
            }

            if (!IPAddress.TryParse(ip, out var address)) {
                return true;
            }

            foreach (var subnet in subnets) {
                if (IPHelper.IsInSubnet(address, subnet)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Checks if access should be blocked based on IP and URL
        /// <param name="user">The user object</param>  
        /// <param name="ip">The IP address</param>
        /// <param name="endpoint">The endpoint. e.g. GET|the/path</param>
        /// </summary>
        public bool IsBlocked(string ip, string endpoint) {
            return IsIPBlocked(ip) || !IsIPAllowed(ip, endpoint);
        }
    }
}
