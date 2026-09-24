using System;
using Aikido.Zen.Core.Helpers;

namespace Aikido.Zen.Core.Models.Events
{
    public class CustomEvent : IEvent
    {
        internal const string EventType = "custom";

        public string Type => EventType;
        public string Name { get; set; }
        public CustomEventRequest Request { get; set; }
        public AgentInfo Agent { get; set; }
        public User User { get; set; }
        public long Time { get; set; }

        public static CustomEvent Create(string name, Context context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return new CustomEvent
            {
                Name = name,
                Request = new CustomEventRequest
                {
                    Method = context.Method,
                    IpAddress = context.RemoteAddress,
                    UserAgent = context.UserAgent,
                    Source = context.Source,
                    Route = context.Route,
                },
                Agent = AgentInfoHelper.GetInfo(),
                User = context.User,
                Time = DateTimeHelper.UTCNowUnixMilliseconds(),
            };
        }
    }

    public class CustomEventRequest
    {
        public string Method { get; set; }
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
        public string Source { get; set; }
        public string Route { get; set; }
    }
}
