using System;
namespace Aikido.Zen.Core.Helpers
{
    public static class RouteHelper
    {
        public static bool MatchRoute(string pattern, string path)
        {
            ReadOnlySpan<char> patternSpan = pattern.TrimStart('/').AsSpan();
            ReadOnlySpan<char> pathSpan = path.TrimStart('/').AsSpan();

            while (!patternSpan.IsEmpty && !pathSpan.IsEmpty)
            {
                var patternSegment = patternSpan.GetNextSegment(out patternSpan);
                var pathSegment = pathSpan.GetNextSegment(out pathSpan);

                if (patternSegment.IsRouteParameter())
                    continue;

                if (!pathSegment.Equals(patternSegment, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return patternSpan.IsEmpty && pathSpan.IsEmpty;
        }
        public static bool IsRouteParameter(this ReadOnlySpan<char> span)
            => span.StartsWith("{".AsSpan()) && span.EndsWith("}".AsSpan());
    }
}
