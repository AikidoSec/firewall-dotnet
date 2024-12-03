namespace Aikido.Zen.Core.Models
{
    public class Heartbeat
    {
        public string Type { get; set; } = "heartbeat";
        public Stats Stats { get; set; }
        public List<Hostname> Hostnames { get; set; }
        public List<Route> Routes { get; set; }
        public List<UserExtended> Users { get; set; }
        public AgentInfo Agent { get; set; }
        public long Time { get; set; }
    }
}