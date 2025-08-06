using System;
using System.Collections.Generic;
using System.Linq;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Core.Models.Events
{
    public class Heartbeat : IEvent
    {
        internal const string EventType = "heartbeat";

        public string Type => EventType;

        public AgentStats Stats { get; set; } = new AgentStats();
        public IEnumerable<AiInfo> Ai { get; set; }
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
                    .ToList(),
                Ai = new List<AiInfo>()
            };
            context.AiStats.CopyProviders((ICollection<AiInfo>)heartbeat.Ai);
            heartbeat.Stats.CopyOperations(context.Stats.Operations);
            heartbeat.Stats.Requests.Total = context.Requests;
            heartbeat.Stats.Requests.Aborted = context.RequestsAborted;
            heartbeat.Stats.Requests.AttacksDetected = new AttacksDetected
            {
                Blocked = context.AttacksBlocked,
                Total = context.AttacksDetected
            };
            heartbeat.MiddlewareInstalled = context.ContextMiddlewareInstalled && context.BlockingMiddlewareInstalled;
            heartbeat.Stats.StartedAt = context.Started;
            heartbeat.Stats.EndedAt = DateTimeHelper.UTCNowUnixMilliseconds();
            return heartbeat;
        }
    }
}
