using System;
using Microsoft.Net.Http.Headers;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// Reads a Content-Type header the way the app's framework does, so any header the app accepts doesn't skip inspection.
    /// </summary>
    internal static class ContentTypeHelper
    {
        /// <summary>
        /// Extracts the type/subtype of a Content-Type header. Matches ASP.NET Core's handling so we don't
        /// skip bodies sent with a header that MediaTypeHeaderValue rejects ("application/json _").
        /// </summary>
        internal static string GetMediaType(string contentType)
        {
            if (string.IsNullOrEmpty(contentType))
            {
                return string.Empty;
            }

            var index = 0;
            var type = ReadToken(contentType, ref index);
            if (type.Length == 0 || !TryConsumeSlash(contentType, ref index))
            {
                return string.Empty;
            }

            var subType = ReadToken(contentType, ref index);
            if (subType.Length == 0)
            {
                return string.Empty;
            }

            return type + "/" + subType;
        }

        /// <summary>
        /// Returns true for a multipart/form-data content type that names a boundary.
        /// </summary>
        internal static bool TryGetMultipartBoundary(string contentType, out string boundary)
        {
            boundary = null;
            if (!MediaTypeHeaderValue.TryParse(contentType, out var parsed) ||
                !parsed.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            boundary = HeaderUtilities.RemoveQuotes(parsed.Boundary).Value;
            return !string.IsNullOrEmpty(boundary);
        }

        private static string ReadToken(string value, ref int index)
        {
            while (index < value.Length && IsOptionalWhiteSpace(value[index]))
            {
                index++;
            }

            var start = index;
            while (index < value.Length && IsTokenChar(value[index]))
            {
                index++;
            }

            return value.Substring(start, index - start);
        }

        // Consumes the '/' between type and subtype, which may have whitespace before it.
        private static bool TryConsumeSlash(string value, ref int index)
        {
            while (index < value.Length && IsOptionalWhiteSpace(value[index]))
            {
                index++;
            }

            if (index >= value.Length || value[index] != '/')
            {
                return false;
            }

            index++;
            return true;
        }

        private const string TokenPunctuation = "!#$%&'*+-.^_`|~";

        // RFC 7230 token character. Anything else ends the type or subtype, which is why this is an allow
        // list: char.IsLetterOrDigit would accept Unicode letters the RFC excludes, and listing delimiters
        // instead would treat every non-ASCII character as part of the subtype.
        private static bool IsTokenChar(char c)
        {
            return (c >= 'a' && c <= 'z')
                || (c >= 'A' && c <= 'Z')
                || (c >= '0' && c <= '9')
                || TokenPunctuation.IndexOf(c) >= 0;
        }

        private static bool IsOptionalWhiteSpace(char c)
        {
            return c == ' ' || c == '\t';
        }
    }
}
