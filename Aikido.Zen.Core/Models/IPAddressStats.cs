using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace Aikido.Zen.Core.Models
{
    /// <summary>
    /// Holds statistics about IP address list matches.
    /// </summary>
    public class IPAddressStats
    {
        /// <summary>
        /// A breakdown of counts per matched IP list key.
        /// </summary>
        [JsonPropertyName("breakdown")]
        public ConcurrentDictionary<string, long> Breakdown { get; set; } = new ConcurrentDictionary<string, long>();
    }
}
