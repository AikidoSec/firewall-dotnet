using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core.Api
{
    /// <summary>
    /// Represents the response from the firewall lists API endpoint.
    /// </summary>
    public class FirewallListsAPIResponse : APIResponse
    {
        public IEnumerable<IPList> BlockedIPAddresses { get; set; }
        public IEnumerable<IPList> AllowedIPAddresses { get; set; }
        public IEnumerable<IPList> BypassedIPAddresses { get; set; }
        public string BlockedUserAgents { get; set; }

        public IEnumerable<IPList> MonitoredIPAddresses { get; set; }
        public string MonitoredUserAgents { get; set; }
        public IEnumerable<UserAgentDetails> UserAgentDetails { get; set; }

        /// <summary>
        /// Gets all blocked IPs from all lists.
        /// </summary>
        [JsonIgnore]
        public IEnumerable<string> BlockedIps => (BlockedIPAddresses ?? Enumerable.Empty<IPList>())
                                .Where(l => l.Ips != null)
                                .SelectMany(l => l.Ips);

        /// <summary>
        /// Gets all allowed IPs from all lists.
        /// </summary>
        [JsonIgnore]
        public IEnumerable<string> AllowedIps => (AllowedIPAddresses ?? Enumerable.Empty<IPList>())
                                .Where(l => l.Ips != null)
                                .SelectMany(l => l.Ips);

        /// <summary>
        /// Gets all bypassed IPs from all lists.
        /// </summary>
        [JsonIgnore]
        public IEnumerable<string> BypassedIps => (BypassedIPAddresses ?? Enumerable.Empty<IPList>())
                                .Where(l => l.Ips != null)
                                .SelectMany(l => l.Ips);

        /// <summary>
        /// Gets the compiled regex for blocked user agents.
        /// </summary>
        [JsonIgnore]
        public Regex BlockedUserAgentsRegex => !string.IsNullOrWhiteSpace(BlockedUserAgents)
            ? new Regex(BlockedUserAgents, RegexOptions.Compiled | RegexOptions.IgnoreCase)
            : null;

        /// <summary>
        /// Represents a list of IPs.
        /// </summary>
        public class IPList
        {
            public string Source { get; set; }
            public string Description { get; set; }
            public IEnumerable<string> Ips { get; set; }
            public string Key { get; set; }
        }

        [JsonConstructor]
        public FirewallListsAPIResponse(
            IEnumerable<IPList> blockedIPAddresses = null,
            IEnumerable<IPList> bypassedIPAddresses = null,
            IEnumerable<IPList> allowedIPAddresses = null,
            string blockedUserAgents = null,
            IEnumerable<IPList> monitoredIPAddresses = null,
            string monitoredUserAgents = null,
            IEnumerable<UserAgentDetails> userAgentDetails = null)
        {
            BlockedIPAddresses = blockedIPAddresses;
            BypassedIPAddresses = bypassedIPAddresses;
            AllowedIPAddresses = allowedIPAddresses;
            BlockedUserAgents = blockedUserAgents;
            MonitoredIPAddresses = monitoredIPAddresses;
            MonitoredUserAgents = monitoredUserAgents;
            UserAgentDetails = userAgentDetails;
        }

        public FirewallListsAPIResponse() { }
    }
}
