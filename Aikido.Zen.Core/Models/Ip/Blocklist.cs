using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Core.Models.Ip
{
    /// <summary>
    /// Manages IP blocklists and allowed subnet rules
    /// </summary>
    public class BlockList
    {
        // The state of our blocklist needs to be thread-safe, as incoming ASP requests can be multithreaded, and the agent, which runs on a background thread, can also update/access the state.
        private IPRange _blockedSubnets = new IPRange();
        private ConcurrentDictionary<string, IPRange> _allowedSubnets = new ConcurrentDictionary<string, IPRange>();
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        /// <summary>
        /// Updates the allowed subnet ranges per URL.
        /// </summary>
        /// <param name="endpoints">The endpoint configurations containing allowed IP addresses.</param>
        public void UpdateAllowedSubnets(IEnumerable<EndpointConfig> endpoints)
        {
            _lock.EnterWriteLock();
            try
            {
                var subnets = endpoints.ToDictionary(
                    x => $"{x.Method}|{x.Route.TrimStart('/')}",
                    x =>
                    {
                        var trie = new IPRange();
                        foreach (var ip in x.AllowedIPAddresses)
                        {
                            foreach (var cidr in IPHelper.ToCidrString(ip))
                            {
                                trie.InsertRange(cidr);
                            }
                        }
                        return trie;
                    }
                );

                _allowedSubnets.Clear();
                foreach (var subnet in subnets)
                {
                    _allowedSubnets.TryAdd(subnet.Key, subnet.Value);
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Updates the blocked subnet ranges.
        /// </summary>
        /// <param name="subnets">The subnet ranges to block.</param>
        public void UpdateBlockedSubnets(IEnumerable<string> subnets)
        {
            _lock.EnterWriteLock();
            try
            {
                _blockedSubnets = new IPRange();
                foreach (var subnet in subnets)
                {
                    foreach (var cidr in IPHelper.ToCidrString(subnet))
                    {
                        _blockedSubnets.InsertRange(cidr);
                    }
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Adds an IP address to the blocklist.
        /// </summary>
        /// <param name="ip">The IP address to block.</param>
        public void AddIpAddressToBlocklist(string ip)
        {
            _lock.EnterWriteLock();
            try
            {
                _blockedSubnets.InsertRange(ip);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Checks if an IP address is blocked.
        /// </summary>
        /// <param name="ip">The IP address to check.</param>
        /// <returns>True if the IP is blocked, false otherwise.</returns>
        public bool IsIPBlocked(string ip)
        {
            _lock.EnterReadLock();
            try
            {
                if (!IPHelper.IsValidIp(ip))
                {
                    return false; // Allow invalid IPs by default
                }

                if (!_blockedSubnets.HasItems)
                    return false; // Allow if no blocked subnets are defined

                return _blockedSubnets.IsIpInRange(ip);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Checks if an IP is allowed for a specific URL.
        /// </summary>
        /// <param name="ip">The IP address to check.</param>
        /// <param name="endpoint">The endpoint, e.g., GET|the/path.</param>
        /// <returns>True if the IP is allowed, false otherwise.</returns>
        public bool IsIPAllowed(string ip, string endpoint)
        {
            _lock.EnterReadLock();
            try
            {
                if (!IPAddress.TryParse(ip, out var parsedIp))
                {
                    return true; // Allow invalid IPs by default
                }

                if (!_allowedSubnets.TryGetValue(endpoint, out var trie))
                {
                    return true; // Allow if no specific subnets are defined for the endpoint
                }

                if (!trie.HasItems)
                {
                    return true; // Allow if no specific subnets are defined for the endpoint
                }

                return trie.IsIpInRange(ip);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Checks if access should be blocked based on IP and URL.
        /// </summary>
        /// <param name="ip">The IP address to check.</param>
        /// <param name="endpoint">The endpoint, e.g., GET|the/path.</param>
        /// <returns>True if access is blocked, false otherwise.</returns>
        public bool IsBlocked(string ip, string endpoint)
        {
            return IsIPBlocked(ip) || !IsIPAllowed(ip, endpoint);
        }
    }
}
