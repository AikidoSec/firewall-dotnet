using System.Collections.Generic;
using System.Text.Json.Serialization;
namespace Aikido.Zen.Core.Models
{
    /// <summary>
    /// Represents a host with hostname and port information.
    /// Inherits from HitCount to track usage for LFU eviction.
    /// </summary>
    public class Host : HitCount
    {
        [JsonPropertyName("hostname")]
        public string Hostname { get; set; }
        [JsonPropertyName("port")]
        public int? Port { get; set; }

        public Host() : base() { }
    }
}
