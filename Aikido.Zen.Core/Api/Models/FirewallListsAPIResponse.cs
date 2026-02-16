using System.Collections.Generic;
using System.Linq;

namespace Aikido.Zen.Core.Api
{
    public class FirewallListsAPIResponse : APIResponse
    {
        /// <summary>
        /// Gets or sets the list of blocked IP addresses.
        /// </summary>
        public IEnumerable<IPList> BlockedIPAddresses { get; set; } = new List<IPList>();

        /// <summary>
        /// Gets or sets the list of allowed IP addresses.
        /// </summary>
        public IEnumerable<IPList> AllowedIPAddresses { get; set; } = new List<IPList>();

        /// <summary>
        /// Gets or sets the list of monitored IP addresses.
        /// </summary>
        public IEnumerable<IPList> MonitoredIPAddresses { get; set; } = new List<IPList>();

        /// <summary>
        /// Gets or sets the blocked user agents as a string. e.g. "googlebot|bingbot|yahoo|aibot"
        /// </summary>
        public string BlockedUserAgents { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the monitored user agents as a regex string.
        /// </summary>
        public string MonitoredUserAgents { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets keyed user-agent regex patterns used for stats attribution.
        /// </summary>
        public IEnumerable<UserAgentDetail> UserAgentDetails { get; set; } = new List<UserAgentDetail>();

        /// <summary>
        /// Gets a collection of blocked IP addresses as strings.
        /// </summary>
        public IEnumerable<string> BlockedIps => (BlockedIPAddresses ?? Enumerable.Empty<IPList>())
                   .Where(ipList => ipList != null)
                   .SelectMany(ipList => ipList.Ips ?? Enumerable.Empty<string>());

        /// <summary>
        /// Gets a collection of allowed IP addresses as strings.
        /// </summary>
        public IEnumerable<string> AllowedIps => (AllowedIPAddresses ?? Enumerable.Empty<IPList>())
                   .Where(ipList => ipList != null)
                   .SelectMany(ipList => ipList.Ips ?? Enumerable.Empty<string>());

        /// <summary>
        /// Gets a collection of monitored IP addresses as strings.
        /// </summary>
        public IEnumerable<string> MonitoredIps => (MonitoredIPAddresses ?? Enumerable.Empty<IPList>())
                   .Where(ipList => ipList != null)
                   .SelectMany(ipList => ipList.Ips ?? Enumerable.Empty<string>());

        public class IPList
        {
            public string Key { get; set; }
            public string Source { get; set; }
            public string Description { get; set; }
            public IEnumerable<string> Ips { get; set; }
        }

        public class UserAgentDetail
        {
            public string Key { get; set; }
            public string Pattern { get; set; }
        }
    }
}
