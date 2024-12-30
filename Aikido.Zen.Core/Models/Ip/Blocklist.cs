using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
        // the state of our blocklist need to be thread safe, because incoming ASP requests can be multithreaded, and the agent, which runs on a background thread can also update / access the state
        private readonly ConcurrentDictionary<string, byte> _blockedAddresses = new ConcurrentDictionary<string, byte>();
        private readonly ConcurrentBag<IPAddressRange> _blockedSubnets = new ConcurrentBag<IPAddressRange>();
        private readonly ConcurrentDictionary<string, IEnumerable<IPAddressRange>> _allowedSubnets = new ConcurrentDictionary<string, IEnumerable<IPAddressRange>>();

        /// <summary>
        /// Updates the allowed subnet ranges per URL
        /// <param name="subnets">The subnet ranges</param>
        /// </summary>
        public void UpdateAllowedSubnets(IEnumerable<EndpointConfig> endpoints)
        {
            var subnets = endpoints.ToDictionary(e => $"{e.Method}|{e.Route.TrimStart('/')}", e => e.AllowedIPAddresses.Select(ip => IPAddressRange.Parse(ip)));
            _allowedSubnets.Clear();
            foreach (var subnet in subnets)
            {
                _allowedSubnets.TryAdd(subnet.Key, subnet.Value);
            }
        }

        /// <summary>
        /// Updates the blocked subnet ranges
        /// <param name="subnets">The subnet ranges</param>
        /// </summary>
        public void UpdateBlockedSubnets(IEnumerable<IPAddressRange> subnets)
        {
            while (!_blockedSubnets.IsEmpty)
            {
                _blockedSubnets.TryTake(out _);
            }
            foreach (var subnet in subnets)
            {
                _blockedSubnets.Add(subnet);
            }
        }

        /// <summary>
        /// Adds an IP address to the blocklist
        /// <param name="ip">The IP address</param>
        /// </summary>
        public void AddIpAddressToBlocklist(string ip)
        {
            _blockedAddresses.TryAdd(ip, 0);
        }

        /// <summary>
        /// Checks if an IP address is blocked
        /// <param name="ip">The IP address</param>
        /// <returns>True if the IP is blocked, false otherwise</returns>
        /// </summary>
        public bool IsIPBlocked(string ip)
        {
            if (_blockedAddresses.ContainsKey(ip))
            {
                return true;
            }

            if (IPAddress.TryParse(ip, out var address))
            {
                return _blockedSubnets.Any(subnet => IPHelper.IsInSubnet(address, subnet));
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

            return  !subnets.Any() || subnets.Any(subnet => IPHelper.IsInSubnet(address, subnet));
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
