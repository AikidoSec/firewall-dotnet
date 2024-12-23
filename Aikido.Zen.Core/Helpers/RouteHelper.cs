using System;
namespace Aikido.Zen.Core.Helpers
{
    public static class RouteHelper
    {
        public static bool MatchRoute(string pattern, string path)
        {
            ReadOnlySpan<char> patternSpan = pattern.TrimStart('/').AsSpan();
            ReadOnlySpan<char> pathSpan = path.TrimStart('/').AsSpan();

            // Remove query and fragment parts from the pattern and path
            int patternQueryIndex = patternSpan.IndexOf('?');
            int patternFragmentIndex = patternSpan.IndexOf('#');
            int patternEndIndex = patternSpan.Length;

            if (patternQueryIndex >= 0 && patternFragmentIndex >= 0)
                patternEndIndex = Math.Min(patternQueryIndex, patternFragmentIndex);
            else if (patternQueryIndex >= 0)
                patternEndIndex = patternQueryIndex;
            else if (patternFragmentIndex >= 0)
                patternEndIndex = patternFragmentIndex;

            patternSpan = patternSpan.Slice(0, patternEndIndex);

            int pathQueryIndex = pathSpan.IndexOf('?');
            int pathFragmentIndex = pathSpan.IndexOf('#');
            int pathEndIndex = pathSpan.Length;

            if (pathQueryIndex >= 0 && pathFragmentIndex >= 0)
                pathEndIndex = Math.Min(pathQueryIndex, pathFragmentIndex);
            else if (pathQueryIndex >= 0)
                pathEndIndex = pathQueryIndex;
            else if (pathFragmentIndex >= 0)
                pathEndIndex = pathFragmentIndex;

            pathSpan = pathSpan.Slice(0, pathEndIndex);

            while (!patternSpan.IsEmpty && !pathSpan.IsEmpty)
            {
                var patternSegment = patternSpan.GetNextSegment(out patternSpan);
                var pathSegment = pathSpan.GetNextSegment(out pathSpan);

                if (patternSegment.IsRouteParameter())
                    continue;

                if (!pathSegment.Equals(patternSegment, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // Ensure both pattern and path are fully matched
            return patternSpan.IsEmpty && pathSpan.IsEmpty;
        }

        public static bool IsRouteParameter(this ReadOnlySpan<char> span)
            => span.StartsWith("{".AsSpan()) && span.EndsWith("}".AsSpan());
    }
}
