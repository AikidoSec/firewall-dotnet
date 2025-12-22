using System;
using System.Collections.Generic;
using System.Linq;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Core.Models.Events
{
    public class Heartbeat : IEvent
    {
        public const string ScheduleId = "heartbeat";
        internal const string EventType = "heartbeat";

        public string Type => EventType;
        public AgentStats Stats { get; set; } = new AgentStats();
        public IEnumerable<AiInfo> Ai { get; set; }
        public IEnumerable<Host> Hostnames { get; set; }
        public IEnumerable<Route> Routes { get; set; }
        public IEnumerable<UserExtended> Users { get; set; }
        public IEnumerable<Package> Packages { get; set; }
        public AgentInfo Agent { get; set; }
        public long Time => DateTimeHelper.UTCNowUnixMilliseconds();
        public bool MiddlewareInstalled { get; set; }

        public static TimeSpan DefaultInterval { get; private set; } = TimeSpan.FromMinutes(10);
        private static int _intervalIndex;
        private static readonly TimeSpan[] StartupIntervals = new[]
        {
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMinutes(2)
        };

        public static TimeSpan GetNextInterval()
        {
            if (_intervalIndex < StartupIntervals.Length)
            {
                return StartupIntervals[_intervalIndex++];
            }

            return DefaultInterval;
        }

        public static Heartbeat Create(AgentContext context)
        {
            if (context.HeartbeatIntervalInMS > 10_000) // safety min 10s
            {
                DefaultInterval = TimeSpan.FromMilliseconds(context.HeartbeatIntervalInMS);
            }

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
            heartbeat.Stats.Requests.AttackWaves = new AttacksDetected
            {
                Blocked = context.AttackWavesBlocked,
                Total = context.AttackWavesDetected
            };
            heartbeat.MiddlewareInstalled = context.ContextMiddlewareInstalled && context.BlockingMiddlewareInstalled;
            heartbeat.Stats.StartedAt = context.Started;
            heartbeat.Stats.EndedAt = DateTimeHelper.UTCNowUnixMilliseconds();
            heartbeat.Packages = context.Packages;
            return heartbeat;
        }
    }
}
