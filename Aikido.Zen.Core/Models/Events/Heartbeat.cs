using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Core.Models.Events
{
    public class Heartbeat : IEvent
    {
        [JsonPropertyName("type")]
        public string Type => "heartbeat";

        [JsonPropertyName("stats")]
        public Stats Stats { get; set; } = new Stats();
        [JsonPropertyName("hostnames")]
        public IEnumerable<Host> Hostnames { get; set; }
        [JsonPropertyName("routes")]
        public IEnumerable<Route> Routes { get; set; }
        [JsonPropertyName("users")]
        public IEnumerable<UserExtended> Users { get; set; }
        [JsonPropertyName("agent")]
        public AgentInfo Agent { get; set; }
        [JsonPropertyName("time")]
        public long Time => DateTimeHelper.UTCNowUnixMilliseconds();
        [JsonPropertyName("middlewareInstalled")]
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
