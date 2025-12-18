using System;
using System.Collections.Generic;
using System.Text.Json;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Vulnerabilities;

namespace Aikido.Zen.Core.Models.Events
{
    public class DetectedAttackWave : IEvent
    {
        public string Type => "detected_attack_wave";
        public RequestInfo Request { get; set; }
        public Attack Attack { get; set; }
        public AgentInfo Agent { get; set; }
        public long Time => DateTimeHelper.UTCNowUnixMilliseconds();

        public static DetectedAttackWave Create(Context context, IEnumerable<SuspiciousRequest> samples)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var request = new RequestInfo
            {
                IpAddress = context.RemoteAddress,
                UserAgent = context.UserAgent,
                Source = context.Source,
            };

            var serializedSamples = JsonSerializer.Serialize(
                samples ?? Array.Empty<SuspiciousRequest>(),
                Api.ZenApi.JsonSerializerOptions);

            var attack = new Attack
            {
                Metadata = new Dictionary<string, object> {
                    { "samples", serializedSamples },
                },
                User = context.User,
            };

            return new DetectedAttackWave
            {
                Request = request,
                Attack = attack,
                Agent = AgentInfoHelper.GetInfo()
            };
        }
    }
}
