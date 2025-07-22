using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace Aikido.Zen.Core.Models
{
    /// <summary>
    /// Holds statistics about user agent pattern matches.
    /// </summary>
    public class UserAgentStats
    {
        /// <summary>
        /// A breakdown of counts per matched user agent key.
        /// </summary>
        public ConcurrentDictionary<string, long> Breakdown { get; set; } = new ConcurrentDictionary<string, long>();
    }
}
