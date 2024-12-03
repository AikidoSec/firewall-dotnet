namespace Aikido.Zen.Core.Models
{
    public class Started
    {
        public string Type { get; set; } = "started";
        public AgentInfo Agent { get; set; }
        public long Time { get; set; }
    }
}
