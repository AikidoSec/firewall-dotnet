using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Core.Models.Events
{
    public class DetectedAttack : IEvent
    {
        [JsonPropertyName("type")]
        public string Type => "detected_attack";
        [JsonPropertyName("request")]
        public RequestInfo Request { get; set; }
        [JsonPropertyName("attack")]
        public Attack Attack { get; set; }
        [JsonPropertyName("agent")]
        public AgentInfo Agent { get; set; }
        [JsonPropertyName("time")]
        public long Time => DateTimeHelper.UTCNowUnixMilliseconds();

        public static DetectedAttack Create(AttackKind kind, Source source, string payload, string operation, Context context, string module, IDictionary<string, object> metadata, bool blocked)
        {

            // if the context is null, throw an argument null exception
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            // in case the body is null, create an empty stream
            if (context.Body == null)
                context.Body = new MemoryStream();

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
                Source = context.Source,
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
