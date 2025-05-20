using System;
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
        private List<(Api.IPList List, IPRange Range)> _blockedIps = new List<(Api.IPList List, IPRange Range)>();
        private List<(Api.IPList List, IPRange Range)> _allowedIps = new List<(Api.IPList List, IPRange Range)>();
        private List<(Api.IPList List, IPRange Range)> _bypassedIps = new List<(Api.IPList List, IPRange Range)>();
        private List<(EndpointConfig Config, IPRange AllowedIPs)> _endpointConfigs = new List<(EndpointConfig Config, IPRange AllowedIPs)>();
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(); // used to make sure only one thread can write to the different parts of the blocklist at a time

        /// <summary>
        /// Updates the allowed ip addresses or ranges per URL.
        /// </summary>
        /// <param name="endpoints">The endpoint configurations containing allowed IP addresses.</param>
        public void UpdateAllowedIpsPerEndpoint(IEnumerable<EndpointConfig> endpoints)
        {
            _lock.EnterWriteLock();
            try
            {
                _endpointConfigs = endpoints.Select(endpoint =>
                {
                    var trie = new IPRange();
                    if (endpoint.AllowedIPAddresses != null)
                    {
                        foreach (var ip in endpoint.AllowedIPAddresses)
                        {
                            foreach (var cidr in IPHelper.ToCidrString(ip))
                            {
                                trie.InsertRange(cidr);
                            }
                        }
                    }
                    return (endpoint, trie);
                }).ToList();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Updates the blocked subnet ranges.
        /// </summary>
        /// <param name="ipLists">The lists of IP addresses or ranges to block.</param>
        public void UpdateBlockedIps(IEnumerable<Api.IPList> ipLists)
        {
            _lock.EnterWriteLock();
            try
            {
                _blockedIps = new List<(Api.IPList List, IPRange Range)>();
                foreach (var ipList in ipLists)
                {
                    var trie = new IPRange();
                    foreach (var ip in ipList.Ips)
                    {
                        foreach (var cidr in IPHelper.ToCidrString(ip))
                        {
                            trie.InsertRange(cidr);
                        }
                    }
                    _blockedIps.Add((ipList, trie));
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Updates the bypassed ip addresses or ranges, they bypass all blocking rules
        /// </summary>
        /// <param name="ipLists">The lists of IP addresses or ranges to be bypassed.</param>
        public void UpdateBypassedIps(IEnumerable<Api.IPList> ipLists)
        {
            _lock.EnterWriteLock();
            try
            {
                _bypassedIps = new List<(Api.IPList List, IPRange Range)>();
                foreach (var ipList in ipLists)
                {
                    var trie = new IPRange();
                    foreach (var ip in ipList.Ips)
                    {
                        foreach (var cidr in IPHelper.ToCidrString(ip))
                        {
                            trie.InsertRange(cidr);
                        }
                    }
                    _bypassedIps.Add((ipList, trie));
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Updates the allowed ip addresses or ranges, they do not bypass any blocking rules
        /// They are used for things like geo-fencing (e.g. only allow IPs from a certain country)
        /// </summary>
        /// <param name="ipLists">The lists of IP addresses or ranges to allow.</param>
        public void UpdateAllowedIps(IEnumerable<Api.IPList> ipLists)
        {
            _lock.EnterWriteLock();
            try
            {
                _allowedIps = new List<(Api.IPList List, IPRange Range)>();
                foreach (var ipList in ipLists)
                {
                    var trie = new IPRange();
                    foreach (var ip in ipList.Ips)
                    {
                        foreach (var cidr in IPHelper.ToCidrString(ip))
                        {
                            trie.InsertRange(cidr);
                        }
                    }
                    _allowedIps.Add((ipList, trie));
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
                var trie = new IPRange();
                trie.InsertRange(ip);
                _blockedIps.Add((new Api.IPList { Source = "Manual Block", Ips = new[] { ip } }, trie));
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
        /// <param name="matchedLists">The ip list(s) that blocked the IP, if any.</param>
        /// <returns>True if the IP is blocked, false otherwise.</returns>
        public bool IsIPBlocked(string ip, out IEnumerable<string> matchedLists)
        {
            matchedLists = Enumerable.Empty<string>();
            _lock.EnterReadLock();
            try
            {
                if (!IPHelper.IsValidIp(ip))
                {
                    return false; // Allow invalid IPs by default
                }

                if (!_blockedIps.Any())
                    return false; // Allow if no blocked subnets are defined

                matchedLists = _blockedIps
                    .Where(x => x.Range.IsIpInRange(ip))
                    .Select(x => x.List.Source);
                return matchedLists.Any();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Checks if an IP is allowed for a specific URL.
        /// </summary>
        /// <param name="context">The context of the request.</param>
        /// <param name="matchedLists">The ip list(s) that allowed the IP, if any.</param>
        /// <returns>True if the IP is allowed, false otherwise.</returns>
        public bool IsIpAllowedForEndpoint(Context context, out IEnumerable<string> matchedLists)
        {
            matchedLists = Enumerable.Empty<string>();
            _lock.EnterReadLock();
            try
            {
                if (!IPAddress.TryParse(context.RemoteAddress, out var parsedIp))
                {
                    return true; // Allow invalid IPs by default
                }

                if (string.IsNullOrWhiteSpace(context.Method) || string.IsNullOrWhiteSpace(context.Url))
                {
                    return true; // Invalid endpoint format, allow by default
                }

                var matchingEndpoints = RouteHelper.MatchEndpoints(context, _endpointConfigs.Select(x => x.Config).ToList());
                if (!matchingEndpoints.Any())
                {
                    return true; // Allow if no specific subnets are defined for the endpoint
                }

                // Check if any matching endpoint allows the IP
                foreach (var matchingEndpoint in matchingEndpoints.Where(e => (e.AllowedIPAddresses?.Count() ?? 0) > 0))
                {
                    var endpointConfig = _endpointConfigs.First(x => x.Config == matchingEndpoint);
                    if (endpointConfig.AllowedIPs.HasItems)
                    {
                        if (!endpointConfig.AllowedIPs.IsIpInRange(context.RemoteAddress))
                        {
                            matchedLists = new[] { matchingEndpoint.Route };
                            return false; // If IP is not allowed for this endpoint, return false
                        }
                    }
                }

                return true; // If no endpoints disallow the IP, return true
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Checks if an IP is bypassed.
        /// </summary>
        /// <param name="ip">The IP address to check.</param>
        /// <param name="matchedLists">The ip list(s) that bypassed the IP, if any.</param>
        /// <returns>True if the IP is bypassed, false otherwise.</returns>
        public bool IsIPBypassed(string ip, out IEnumerable<string> matchedLists)
        {
            matchedLists = Enumerable.Empty<string>();
            _lock.EnterReadLock();
            try
            {
                if (!IPHelper.IsValidIp(ip))
                {
                    return false; // Invalid IPs are not allowed, since allowing bypasses other blocking rules
                }

                matchedLists = _bypassedIps
                    .Where(x => x.Range.IsIpInRange(ip))
                    .Select(x => x.List.Source);
                return matchedLists.Any();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Checks if an IP is allowed.
        /// </summary>
        /// <param name="ip">The IP address to check.</param>
        /// <param name="matchedLists">The ip list(s) that allowed the IP, if any.</param>
        /// <returns>True if the IP is allowed, false otherwise.</returns>
        public bool IsIPAllowed(string ip, out IEnumerable<string> matchedLists)
        {
            matchedLists = Enumerable.Empty<string>();
            _lock.EnterReadLock();
            try
            {
                if (!_allowedIps.Any())
                {
                    return true; // If no allowed IPs are defined, all IPs are allowed
                }

                if (!IPHelper.IsValidIp(ip))
                {
                    return false; // Invalid IPs are not allowed when there are allowed IPs
                }

                matchedLists = _allowedIps
                    .Where(x => x.Range.IsIpInRange(ip))
                    .Select(x => x.List.Source);
                return matchedLists.Any();
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Checks if access should be blocked based on IP and URL.
        /// </summary>
        /// <param name="context">The context of the request.</param>
        /// <param name="reason">The reason for blocking, if any.</param>
        /// <param name="matchedLists">Dictionary containing the matched IPLists by type.</param>
        /// <returns>True if access is blocked, false otherwise.</returns>
        public bool IsBlocked(Context context, out string reason, out Dictionary<string, IEnumerable<string>> matchedLists)
        {
            reason = null;
            matchedLists = new Dictionary<string, IEnumerable<string>>();

            if (IsPrivateOrLocalIp(context.RemoteAddress))
            {
                reason = "IP is private or local";
                return false;
            }

            if (IsIPBypassed(context.RemoteAddress, out var bypassedLists))
            {
                reason = "IP is bypassed";
                matchedLists["bypass"] = bypassedLists;
                return false;
            }

            if (IsIPBlocked(context.RemoteAddress, out var blockedLists))
            {
                reason = "IP is blocked";
                matchedLists["blocking"] = blockedLists;
                return true;
            }

            if (!IsIpAllowedForEndpoint(context, out var endpointLists))
            {
                reason = "IP is not allowed for this endpoint";
                matchedLists["endpoint"] = endpointLists;
                return true;
            }

            if (!IsIPAllowed(context.RemoteAddress, out var allowedLists))
            {
                reason = "IP is not allowed";
                matchedLists["allow"] = allowedLists;
                return true;
            }

            return false;
        }

        internal bool IsPrivateOrLocalIp(string ip)
        {
            return IPHelper.IsPrivateOrLocalIp(ip);
        }
    }
}
