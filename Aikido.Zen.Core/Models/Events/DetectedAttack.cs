using Aikido.Zen.Core.Helpers;
using System.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aikido.Zen.Core.Models.Events {
    public class DetectedAttack : IEvent
    {
        public string Type => "detected_attack";
        public RequestInfo Request { get; set; }
        public Attack Attack { get; set; }
        public AgentInfo Agent { get; set; }
        public long Time => DateTimeHelper.UTCNowUnixMilliseconds();

        public static DetectedAttack Create(AttackKind kind, Source source, string payload, string operation, Context context, string module, IDictionary<string, object> metadata, bool blocked) {
            var path = "";
            if (Uri.TryCreate(context.Url, UriKind.Absolute, out var uri))
                path = uri.AbsolutePath;
            else
                path = context.Url;

            var stackTrace = new StackTrace().ToString();
            stackTrace = stackTrace.Substring(0, Math.Min(stackTrace.Length, 4096));
            var attack = new Attack
            {
                Blocked = blocked,
                Kind = kind.ToJsonName(),
                Module = module, // the qualified assembly name
                Path = path,
                User = context.User,
                Payload = payload,
                Operation = operation, // the class + method where the attack was detected
                Metadata = metadata,
                Stack = stackTrace,
                Source = source.ToJsonName()
            };

            var request = new RequestInfo
            {
                Headers = context.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value)),
                Method = context.Method,
                Source = Environment.Version.Major >= 5 ? "DotNetCore" : "DotNetFramework",
                Url = context.Url,
                Body = HttpHelper.GetRawBody(context.Body),
                Route = context.Route,
                IpAddress = context.RemoteAddress,
                UserAgent = context.UserAgent
            };
            return new DetectedAttack
            {
                Attack = attack,
                Request = request,
                Agent = AgentInfoHelper.GetInfo()
            };
        }

    }
}
