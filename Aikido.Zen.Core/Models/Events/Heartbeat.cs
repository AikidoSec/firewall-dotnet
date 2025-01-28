using Aikido.Zen.Core.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

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
        public bool MiddlewareInstalled { get; set; }

        // Constants for the heartbeat event
        public const string ScheduleId = "heartbeat";
#if DEBUG
        public static TimeSpan Interval => TimeSpan.FromMinutes(1);
#else
        public static TimeSpan Interval => TimeSpan.FromMinutes(10);
#endif

        public static Heartbeat Create(AgentContext context)
        {
            var heartbeat = new Heartbeat
            {
                Agent = AgentInfoHelper.GetInfo(),
                Hostnames = context.Hostnames
                    .ToList(),
                Users = context.Users
                    .ToList(),
                Routes = context.Routes
                    .ToList()
            };
            heartbeat.Stats.Requests.Total = context.Requests;
            heartbeat.Stats.Requests.Aborted = context.RequestsAborted;
            heartbeat.Stats.Requests.AttacksDetected = new AttacksDetected
            {
                Blocked = context.AttacksBlocked,
                Total = context.AttacksDetected
            };
            heartbeat.Stats.StartedAt = context.Started;
            heartbeat.Stats.EndedAt = DateTimeHelper.UTCNowUnixMilliseconds();
            heartbeat.MiddlewareInstalled = context.ContextMiddlewareInstalled && context.BlockingMiddlewareInstalled;
            return heartbeat;
        }
    }
}
