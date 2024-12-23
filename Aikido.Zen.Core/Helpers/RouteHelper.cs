using System;
namespace Aikido.Zen.Core.Helpers
{
    public static class RouteHelper
    {
        /// <summary>
        /// Matches a route pattern against an actual URL path
        /// </summary>
        /// <param name="pattern">The route pattern (e.g. "api/users/{id}")</param>
        /// <param name="path">The actual URL path to match against</param>
        /// <returns>True if the path matches the pattern, false otherwise</returns>
        public static bool MatchRoute(string pattern, string path)
        {
            // Convert strings to spans and trim leading slashes for consistent comparison
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

            // Continue comparing segments while both pattern and path have content
            while (!patternSpan.IsEmpty && !pathSpan.IsEmpty)
            {
                // Get the next segment from both pattern and path
                // e.g. for "api/users/{id}", first segment is "api"
                var patternSegment = patternSpan.GetNextSegment(out patternSpan);
                var pathSegment = pathSpan.GetNextSegment(out pathSpan);

                // If pattern segment is a route parameter (e.g. {id}),
                // skip comparison since any value is valid
                if (patternSegment.IsRouteParameter())
                    continue;

                // If segments don't match (ignoring case), route doesn't match
                if (!pathSegment.Equals(patternSegment, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // Route only matches if both pattern and path are fully consumed
            return patternSpan.IsEmpty && pathSpan.IsEmpty;
        }

        /// <summary>
        /// Checks if a route segment is a parameter (enclosed in curly braces)
        /// </summary>
        /// <param name="span">The route segment to check</param>
        /// <returns>True if segment is a parameter (e.g. {id}), false otherwise</returns>
        public static bool IsRouteParameter(this ReadOnlySpan<char> span)
            => span.StartsWith("{".AsSpan()) && span.EndsWith("}".AsSpan());
    }
}
