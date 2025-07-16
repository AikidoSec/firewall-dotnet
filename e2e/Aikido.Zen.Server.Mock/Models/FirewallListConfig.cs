using System.Collections.Generic;

namespace Aikido.Zen.Server.Mock.Models
{
    /// <summary>
    /// Represents the configuration for firewall lists.
    /// </summary>
    public class FirewallListConfig
    {
        /// <summary>
        /// Gets or sets a value indicating whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the service identifier.
        /// </summary>
        public int ServiceId { get; set; }

        public IEnumerable<IPList> BlockedIPAddresses { get; set; }
        public IEnumerable<IPList> BypassedIPAddresses { get; set; }
        public string BlockedUserAgents { get; set; }
        public IEnumerable<IPList> AllowedIPAddresses { get; set; }
        public IEnumerable<IPList> MonitoredIPAddresses { get; set; }
        public string MonitoredUserAgents { get; set; }
        public IEnumerable<UserAgentDetails> UserAgentDetails { get; set; }


        /// <summary>
        /// Represents a list of IP addresses with a source and description.
        /// </summary>
        public class IPList
        {
            /// <summary>
            /// Gets or sets the source of the IP list.
            /// </summary>
            public string Source { get; set; }

            /// <summary>
            /// Gets or sets the description of the IP list.
            /// </summary>
            public string Description { get; set; }

            /// <summary>
            /// Gets or sets the list of IP addresses.
            /// </summary>
            public string[] Ips { get; set; }
            public string Key { get; set; }
        }

    }
}
