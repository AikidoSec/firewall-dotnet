using Aikido.Zen.Core.Helpers;
using System;

namespace Aikido.Zen.Core.Models.Events
{
    public class Started : IEvent
    {
        internal const string StartedEventName = "started";

        public string Type => StartedEventName;
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
