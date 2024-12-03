namespace Aikido.Zen.Core.Models
{
    public class Stats
    {
        public Dictionary<string, MonitoredSinkStats> Sinks { get; set; }
        public long StartedAt { get; set; }
        public long EndedAt { get; set; }
        public Requests Requests { get; set; }
    }
}