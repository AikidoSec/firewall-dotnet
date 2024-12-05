using Aikido.Zen.Core.Models;
using System;

namespace Aikido.Zen.Core.EventHandling
{
    /// <summary>
    /// Event fired when an inbound request is received
    /// </summary>
    public class InboundRequestEvent : IAppEvent<InboundRequestEvent.RequestContext>
    {
        public RequestContext Data { get; }
        public string EventType { get; }
        public System.DateTime CreatedAt { get; }

        public InboundRequestEvent(User user, string url, string method, string ipAddress)
        {
            Data = new RequestContext
            {
                User = user,
                Url = url,
                Method = method,
                IpAddress = ipAddress
            };
            CreatedAt = DateTime.UtcNow;
            EventType = nameof(InboundRequestEvent);
        }

        public class RequestContext
        {
            public User User { get; set; }
            public string Url { get; set; }
            public string Method { get; set; }
            public string IpAddress { get; set; }
        }
    }
}
