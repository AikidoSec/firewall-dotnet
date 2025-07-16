namespace Aikido.Zen.Server.Mock.Models
{
    /// <summary>
    /// Represents the details of a user agent list for monitoring or blocking.
    /// </summary>
    public class UserAgentDetails
    {
        /// <summary>
        /// Gets or sets the key or name of the user agent list (e.g., "crawlers").
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Gets or sets the regex pattern for matching user agents in this list.
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this list is for monitoring.
        /// </summary>
        public bool Monitored { get; set; }
    }
}
