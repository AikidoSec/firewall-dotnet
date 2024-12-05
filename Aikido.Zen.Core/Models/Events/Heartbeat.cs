using Aikido.Zen.Core.Helpers;
using System;
using System.Collections.Generic;

namespace Aikido.Zen.Core.Models.Events
{
    public class Heartbeat : IEvent
    {
        public string Type => "heartbeat";

        public Stats Stats { get; set; } = new Stats();
        public IEnumerable<Host> Hostnames { get; set; }
        public IEnumerable<Route> Routes { get; set; }
        public IEnumerable<UserExtended> Users { get; set; }
        public AgentInfo Agent { get; set; }
        public long Time => DateTimeHelper.UTCNowUnixMilliseconds();

        // Constants for the heartbeat event
        public const string ScheduleId = "heartbeat";
#if DEBUG
        public static TimeSpan Interval => TimeSpan.FromMinutes(1);
#else
        public static TimeSpan Interval => TimeSpan.FromMinutes(10);
#endif
    }
}
