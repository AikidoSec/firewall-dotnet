using System;
using System.Collections.Generic;
using System.IO;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Test.Mocks
{
    public static class ContextMock
    {
        public static Context CreateMock()
        {
            return new Context
            {
                Url = "https://example.com",
                Method = "GET",
                Query = new Dictionary<string, string[]>(),
                Headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase),
                RouteParams = new Dictionary<string, string>(),
                RemoteAddress = "127.0.0.1",
                Body = new MemoryStream(),
                Cookies = new Dictionary<string, string>(),
                AttackDetected = false,
                ConsumedRateLimitForIP = false,
                ConsumedRateLimitForUser = false,
                User = null,
                Source = "test",
                Route = "/",
                Graphql = Array.Empty<string>(),
                Xml = null,
                Subdomains = Array.Empty<string>(),
                Cache = new Dictionary<string, HashSet<string>>(),
                OutgoingRequestRedirects = new List<Context.RedirectInfo>(),
                ParsedUserInput = new Dictionary<string, string>(),
                UserAgent = "test-agent"
            };
        }

        public static Context CreateMockWithData(
            string url = null,
            string method = null, 
            IDictionary<string, string[]> headers = null,
            IDictionary<string, string> cookies = null,
            Stream body = null,
            User user = null,
            string source = null,
            IDictionary<string, string> parsedUserInput = null,
            string userAgent = null)
        {
            var context = CreateMock();

            if (url != null) context.Url = url;
            if (method != null) context.Method = method;
            if (headers != null) context.Headers = headers;
            if (cookies != null) context.Cookies = cookies;
            if (body != null) context.Body = body;
            if (user != null) context.User = user;
            if (source != null) context.Source = source;
            if (parsedUserInput != null) context.ParsedUserInput = parsedUserInput;
            if (userAgent != null) context.UserAgent = userAgent;

            return context;
        }
    }
}
