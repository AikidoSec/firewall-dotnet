using Aikido.Zen.Core.Helpers;
using System;

namespace Aikido.Zen.Core.Models.Events
{
    public class Started : IEvent
    {
        internal const string EventType = "started";

        public string Type => EventType;
        public AgentInfo Agent { get; set; }
        public long Time => DateTimeHelper.UTCNowUnixMilliseconds();

        public static Started Create() {
            return new Started
            {
                Agent = AgentInfoHelper.GetInfo()
            };
        }
    }
}
