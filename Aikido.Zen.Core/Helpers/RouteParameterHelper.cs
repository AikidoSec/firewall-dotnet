using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// Helper class for detecting and converting URL segments into parameterized routes
    /// </summary>
    public static class RouteParameterHelper
    {
        private static readonly char[] LowercaseChars = "abcdefghijklmnopqrstuvwxyz".ToCharArray();
        private static readonly char[] UppercaseChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
        private static readonly char[] Numbers = "0123456789".ToCharArray();
        private static readonly char[] SpecialChars = "!#$%^&*|;:<>".ToCharArray();
        private static readonly string[] KnownWordSeparators = new[] { "-" };
        private const int MinimumSecretLength = 10;

        // Cached regex patterns for better performance
        private static readonly Regex UuidRegex = new Regex(@"^(?:[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}|00000000-0000-0000-0000-000000000000|ffffffff-ffff-ffff-ffff-ffffffffffff)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ObjectIdRegex = new Regex(@"^[0-9a-f]{24}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex UlidRegex = new Regex(@"^[0-9A-HJKMNP-TV-Z]{26}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex NumberRegex = new Regex(@"^\d+$", RegexOptions.Compiled);
        private static readonly Regex DateRegex = new Regex(@"^\d{4}-\d{2}-\d{2}|\d{2}-\d{2}-\d{4}$", RegexOptions.Compiled);
        private static readonly Regex EmailRegex = new Regex(@"^[a-zA-Z0-9.!#$%&'*+\/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$", RegexOptions.Compiled);
        private static readonly Regex HashRegex = new Regex(@"^(?:[a-f0-9]{32}|[a-f0-9]{40}|[a-f0-9]{64}|[a-f0-9]{128})$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly int[] HashLengths = new[] { 32, 40, 64, 128 };

        /// <summary>
        /// Builds a parameterized route from a URL by detecting and replacing segments with parameter placeholders
        /// </summary>
        /// <param name="url">The URL to convert into a parameterized route</param>
        /// <returns>A parameterized route string, or null if the URL cannot be parsed</returns>
        public static string BuildRouteFromUrl(string url)
        {
            try
            {
                var kind = url.StartsWith("/") ? UriKind.Relative : UriKind.Absolute;
                if (!Uri.TryCreate(url, kind, out var uri))
                    return null;
                var path = uri.IsAbsoluteUri ? uri.AbsolutePath : uri.OriginalString;

                if (string.IsNullOrEmpty(path))
                    return null;

                if (path == "/")
                    return "/";

                var route = string.Join("/",
                    path.Split('/')
                        .Where(s => !string.IsNullOrEmpty(s))
                        .Select(ReplaceUrlSegmentWithParam));

                if (route.EndsWith("/"))
                    route = route.Substring(0, route.Length - 1);
                return "/" + route.TrimStart('/');
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Determines if a string appears to be a secret based on character composition and patterns
        /// </summary>
        public static bool LooksLikeASecret(string str)
        {
            if (str.Length <= MinimumSecretLength)
                return false;

            if (!Numbers.Any(c => str.Contains(c)))
                return false;

            var hasLower = LowercaseChars.Any(c => str.Contains(c));
            var hasUpper = UppercaseChars.Any(c => str.Contains(c));
            var hasSpecial = SpecialChars.Any(c => str.Contains(c));
            var charsets = new[] { hasLower, hasUpper, hasSpecial };

            // Check if at least 2 different charsets are present
            if (charsets.Count(x => x) < 2)
                return false;

            if (str.Contains(' '))
                return false;

            if (KnownWordSeparators.Any(sep => str.Contains(sep)))
                return false;

            // Check if it's a file with extension (e.g., "index.BRaz9DSe.css")
            var lastDotIndex = str.LastIndexOf('.');
            if (lastDotIndex > 0)
            {
                var extension = str.Substring(lastDotIndex + 1);
                if (extension.Length > 1 && extension.Length < 6)
                    return false;
            }

            // Check character uniqueness in windows
            var windowSize = MinimumSecretLength;
            var ratios = new List<double>();

            for (var i = 0; i <= str.Length - windowSize; i++)
            {
                var window = str.Substring(i, windowSize);
                var uniqueChars = new HashSet<char>(window);
                ratios.Add((double)uniqueChars.Count / windowSize);
            }

            var averageRatio = ratios.Average();
            return averageRatio > 0.75;
        }

        private static string ReplaceUrlSegmentWithParam(string segment)
        {
            if (string.IsNullOrEmpty(segment))
                return segment;

            if (char.IsDigit(segment[0]))
            {
                if (NumberRegex.IsMatch(segment))
                    return ":number";

                if (DateRegex.IsMatch(segment))
                    return ":date";
            }

            if (segment.Length == 36 && UuidRegex.IsMatch(segment))
                return ":uuid";

            if (segment.Length == 26 && UlidRegex.IsMatch(segment))
                return ":ulid";

            if (segment.Length == 24 && ObjectIdRegex.IsMatch(segment))
                return ":objectId";

            if (segment.Contains('@') && EmailRegex.IsMatch(segment))
                return ":email";

            if ((segment.Contains(':') || segment.Contains('.')) && IPAddress.TryParse(segment, out _))
                return ":ip";

            if (HashLengths.Contains(segment.Length) && HashRegex.IsMatch(segment))
                return ":hash";

            if (LooksLikeASecret(segment))
                return ":secret";

            return segment;
        }
    }
}
