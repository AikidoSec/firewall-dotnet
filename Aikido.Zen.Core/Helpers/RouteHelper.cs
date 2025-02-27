using System;
using System.IO;
using System.Linq;

namespace Aikido.Zen.Core.Helpers
{
    public static class RouteHelper
    {


        private static readonly string[] ExcludedMethods = { "OPTIONS", "HEAD" };
        private static readonly string[] IgnoreExtensions = { "properties", "php", "asp", "aspx", "jsp", "config" };
        private static readonly string[] IgnoreStrings = { "cgi-bin" };


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


        /// <summary>
        /// Determines if a route should be added based on the context and HTTP status code.
        /// </summary>
        /// <param name="context">The context containing route and method information.</param>
        /// <param name="httpStatusCode">The HTTP status code of the request.</param>
        /// <returns>True if the route should be added, false otherwise.</returns>
        public static bool ShouldAddRoute(Context context, int httpStatusCode)
        {
            // Check for null context
            if (context == null)
            {
                return false;
            }

            // Check if the status code is valid
            bool validStatusCode = httpStatusCode >= 200 && httpStatusCode <= 399;
            if (!validStatusCode)
            {
                return false;
            }

            // Check if the method is excluded
            if (context.Method == null || ExcludedMethods.Contains(context.Method))
            {
                return false;
            }

            // Check for null or empty route
            if (string.IsNullOrEmpty(context.Route))
            {
                return false;
            }

            // Split the route into segments
            var segments = context.Route.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            // Check for dot files and ignored strings
            if (segments.Any(s => IsDotFile(s) && !ContainsIgnoredString(s)))
            {
                return false;
            }

            // Ensure all segments have allowed extensions
            return segments.All(IsAllowedExtension);
        }

        private static bool IsAllowedExtension(string segment)
        {
            string extension = Path.GetExtension(segment);
            if (!string.IsNullOrEmpty(extension))
            {
                extension = extension.TrimStart('.');
                if (extension.Length >= 2 && extension.Length <= 5 || IgnoreExtensions.Contains(extension))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsDotFile(string segment)
        {
            // See https://www.rfc-editor.org/rfc/rfc8615
            if (segment == ".well-known")
            {
                return false;
            }
            return segment.StartsWith(".") && segment.Length > 1;
        }

        private static bool ContainsIgnoredString(string segment)
        {
            return IgnoreStrings.Any(str => segment.Contains(str));
        }
    }
}
