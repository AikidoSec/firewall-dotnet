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

        /// <summary>
        /// Gets or sets the list of blocked IP addresses.
        /// </summary>
        public List<IPList> BlockedIPAddresses { get; set; }

        /// <summary>
        /// Gets or sets the list of allowed IP addresses.
        /// </summary>
        public List<IPList> AllowedIPAddresses { get; set; }

        /// <summary>
        /// Gets or sets the blocked user agents.
        /// </summary>
        public string BlockedUserAgents { get; set; }

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
            public List<string> Ips { get; set; }
        }
    }
}
