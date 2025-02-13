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
        private IPRange _blockedIps = new IPRange();
        private IPRange _allowedIps = new IPRange();
        private ConcurrentDictionary<string, IPRange> _allowedIpsForEndpoint = new ConcurrentDictionary<string, IPRange>();
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        /// <summary>
        /// Updates the allowed subnet ranges per URL.
        /// </summary>
        /// <param name="endpoints">The endpoint configurations containing allowed IP addresses.</param>
        public void UpdateAllowedIpsForEndpoint(IEnumerable<EndpointConfig> endpoints)
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

                _allowedIpsForEndpoint.Clear();
                foreach (var subnet in subnets)
                {
                    _allowedIpsForEndpoint.TryAdd(subnet.Key, subnet.Value);
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
        /// <param name="ips">The subnet ranges to block.</param>
        public void UpdateBlockedIps(IEnumerable<string> ips)
        {
            _lock.EnterWriteLock();
            try
            {
                _blockedIps = new IPRange();
                foreach (var subnet in ips)
                {
                    foreach (var cidr in IPHelper.ToCidrString(subnet))
                    {
                        _blockedIps.InsertRange(cidr);
                    }
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Updates the allowed ip addresses or ranges, they bypass all blocking rules
        /// </summary>
        /// <param name="ips">The ip addresses or ranges to allow.</param>
        public void UpdateAllowedIps(IEnumerable<string> ips)

        {
            _lock.EnterWriteLock();
            try
            {
                _allowedIps = new IPRange();
                foreach (var subnet in ips)
                {

                    foreach (var cidr in IPHelper.ToCidrString(subnet))
                    {
                        _allowedIps.InsertRange(cidr);
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
                _blockedIps.InsertRange(ip);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Checks if an IP address is blocked. (e.g. due to geo restrictions, known malicious IPs, etc.)
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

                if (!_blockedIps.HasItems)
                    return false; // Allow if no blocked subnets are defined

                return _blockedIps.IsIpInRange(ip);
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
        public bool IsIPAllowedForEndpoint(string ip, string endpoint)
        {
            _lock.EnterReadLock();
            try
            {
                if (!IPAddress.TryParse(ip, out var parsedIp))
                {
                    // If no specific subnets are defined for the endpoint, allow invalid IPs, otherwise block them
                    return _allowedIpsForEndpoint.Count == 0;
                }

                if (!_allowedIpsForEndpoint.TryGetValue(endpoint, out var trie))
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

        public bool IsIPAllowed(string ip)
        {
            _lock.EnterReadLock();
            try
            {
                if (_allowedIps.HasItems)
                {
                    if (IPHelper.IsValidIp(ip))
                    {
                        return _allowedIps.IsIpInRange(ip);
                    }
                    return false;
                }

                return true;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        internal bool IsPrivateOrLocalIp(string ip)
        {
            return IPHelper.IsPrivateOrLocalIp(ip);
        }

        /// <summary>
        /// Checks if access should be blocked based on IP and URL.
        /// </summary>
        /// <param name="ip">The IP address to check.</param>
        /// <param name="endpoint">The endpoint, e.g., GET|the/path.</param>
        /// <returns>True if access is blocked, false otherwise.</returns>
        public bool IsBlocked(string ip, string endpoint, out string reason)
        {
            reason = "";
            if (IsPrivateOrLocalIp(ip))
            {
                reason = "Private or local IP";
                return false;
            }

            if (!IsIPAllowed(ip))
            {
                reason = "IP is not allowed";
                return true;
            }

            if (!IsIPAllowedForEndpoint(ip, endpoint))
            {
                reason = "IP is not allowed for endpoint";
                return true;
            }

            if (IsIPBlocked(ip))
            {
                reason = "IP is blocked";
                return true;
            }


            reason = "IP is allowed";
            return false;
        }
    }
}
