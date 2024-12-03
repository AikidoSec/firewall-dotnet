using System.Collections.Generic;

namespace Aikido.Zen.Core.Models.Events
{
    public class Heartbeat : IEvent
    {
        public const string Type = "heartbeat";
        public Stats Stats { get; set; }
        public IEnumerable<Host> Hostnames { get; set; }
        public IEnumerable<Route> Routes { get; set; }
        public List<UserExtended> Users { get; set; }
        public AgentInfo Agent { get; set; }
        public long Time { get; set; }
    }
}