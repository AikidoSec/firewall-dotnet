using System.Collections.Generic;

namespace Aikido.Zen.Core.Models
{
    /// <summary>
    /// Represents a host with hostname and port information.
    /// Inherits from HitCount to track usage for LFU eviction.
    /// </summary>
    public class Host : HitCount
    {
        public string Hostname { get; set; }
        public int? Port { get; set; }

        public Host() : base() { }
    }
}
