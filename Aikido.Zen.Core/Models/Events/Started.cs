using System;

namespace Aikido.Zen.Core.Models.Events
{
    public class Started : IEvent
    {
        public const string Type = "started";
        public AgentInfo Agent { get; set; }
        public long Time => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
