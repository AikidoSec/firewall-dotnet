using Aikido.Zen.Core.Helpers;
using System;

namespace Aikido.Zen.Core.Models.Events
{
    public class Started : IEvent
    {
        public string Type => "started";
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
