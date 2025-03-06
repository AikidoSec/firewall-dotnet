using Aikido.Zen.Core.Models;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Aikido.Zen.Core.Api
{
    /// <summary>
    /// Represents the response from the Reporting API.
    /// </summary>
    public class ReportingAPIResponse : APIResponse
    {
        /// <summary>
        /// Gets or sets the timestamp when the configuration was last updated.
        /// </summary>
        public long ConfigUpdatedAt { get; set; }

        /// <summary>
        /// Gets or sets the interval for heartbeat in milliseconds.
        /// </summary>
        public int HeartbeatIntervalInMS { get; set; }

        /// <summary>
        /// Gets or sets the collection of endpoint configurations.
        /// </summary>
        public IEnumerable<EndpointConfig> Endpoints { get; set; } = new List<EndpointConfig>();

        /// <summary>
        /// Gets or sets the collection of blocked user IDs.
        /// </summary>
        public IEnumerable<string> BlockedUserIds { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the collection of bypassed IP addresses.
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("allowedIPAddresses")]
        public IEnumerable<string> BypassedIPAddresses { get; set; } = new List<string>(); // we call this bypassed ip addresses, to aovid confusion with allowed ip addresses present on the lists endpoint

        /// <summary>
        /// Gets or sets the blocked user agents as a comma-separated list of regex patterns.
        /// e.g. "googlebot|bingbot|yahoo|aibot"
        /// </summary>
        public string BlockedUserAgents { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether any statistics were received.
        /// </summary>
        public bool ReceivedAnyStats { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether blocking is enabled.
        /// </summary>
        public bool Block { get; set; }

        /// <summary>
        /// Gets the regex pattern for blocked user agents.
        /// </summary>
        public Regex BlockedUserAgentsRegex => BlockedUserAgents != null ? new Regex(BlockedUserAgents) : null;
    }
}
