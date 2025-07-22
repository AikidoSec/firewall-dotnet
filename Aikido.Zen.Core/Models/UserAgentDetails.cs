namespace Aikido.Zen.Core.Models
{
    /// <summary>
    /// Represents the details of a user agent pattern to be monitored.
    /// </summary>
    public class UserAgentDetails
    {
        /// <summary>
        /// The key or identifier for the user agent pattern.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// The regex pattern to match against the user agent string.
        /// </summary>
        public string Pattern { get; set; }
    }
}
