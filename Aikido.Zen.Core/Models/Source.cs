using System;

namespace Aikido.Zen.Core.Models
{
    public enum Source
    {
        Query,
        Body,
        Headers,
        Cookies,
        RouteParams,
        Graphql,
        Xml,
        Subdomains
    }

    public static class SourceExtensions
    {
        public static string ToHumanName(this Source source)
        {
            switch (source)
            {
                case Source.Query:
                    return "query parameters";
                case Source.Body:
                    return "request body";
                case Source.Headers:
                    return "HTTP headers";
                case Source.Cookies:
                
                    return "cookies";
                case Source.RouteParams:
                    return "route parameters";
                case Source.Graphql:
                    return "GraphQL query";
                case Source.Xml:
                    return "XML content";
                case Source.Subdomains:
                    return "subdomains";
                default:
                    throw new ArgumentOutOfRangeException(nameof(source));
            }
        }
    }
}
