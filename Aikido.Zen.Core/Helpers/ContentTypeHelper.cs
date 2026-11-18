using System;
using System.Text;

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
        /// Returns the value of a parameter such as charset or boundary, without surrounding quotes, or null when it is missing.
        /// </summary>
        internal static string GetParameter(string contentType, string name)
        {
            if (string.IsNullOrEmpty(contentType))
            {
                return null;
            }

            // The media type is skipped up to its first ';', so malformed text in it can't hide the parameters.
            var separator = contentType.IndexOf(';');
            while (separator >= 0)
            {
                var start = separator + 1;
                var end = FindParameterEnd(contentType, start);
                var parameter = contentType.Substring(start, end - start);
                var equals = parameter.IndexOf('=');
                if (equals > 0 && parameter.Substring(0, equals).Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return Unquote(parameter.Substring(equals + 1).Trim());
                }

                separator = end < contentType.Length ? end : -1;
            }

            return null;
        }

        /// <summary>
        /// Returns true for a multipart/form-data content type that names a boundary.
        /// </summary>
        internal static bool TryGetMultipartBoundary(string contentType, out string boundary)
        {
            boundary = null;
            if (!GetMediaType(contentType).Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            boundary = GetParameter(contentType, "boundary");
            return !string.IsNullOrEmpty(boundary);
        }

        // Finds the next ';' that is not inside a quoted value, or the end of the header.
        private static int FindParameterEnd(string value, int start)
        {
            var inQuotes = false;
            for (var i = start; i < value.Length; i++)
            {
                if (inQuotes)
                {
                    if (value[i] == '\\')
                    {
                        i++;
                    }
                    else if (value[i] == '"')
                    {
                        inQuotes = false;
                    }
                }
                else if (value[i] == '"')
                {
                    inQuotes = true;
                }
                else if (value[i] == ';')
                {
                    return i;
                }
            }

            return value.Length;
        }

        private static string Unquote(string value)
        {
            if (value.Length == 0 || value[0] != '"')
            {
                return value;
            }

            var unquoted = new StringBuilder();
            for (var i = 1; i < value.Length && value[i] != '"'; i++)
            {
                if (value[i] == '\\' && i + 1 < value.Length)
                {
                    i++;
                }

                unquoted.Append(value[i]);
            }

            return unquoted.ToString();
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
