using System;

namespace Aikido.Zen.Core.Helpers
{
    public static class StringHelper
    {
        public static bool Contains(this ReadOnlySpan<char> source, ReadOnlySpan<char> value)
        {
            // Check if the value is empty, or larger than the source
            if (value.IsEmpty)
            {
                return true;
            }

            if (value.Length > source.Length)
            {
                return false;
            }

            // Loop through the source and compare spans
            for (int i = 0; i <= source.Length - value.Length; i++)
            {
                if (source.Slice(i, value.Length).SequenceEqual(value))
                {
                    return true;
                }
            }

            return false;
        }


        /// <summary>
        /// Gets the next segment from a URL path by splitting on forward slashes.
        /// </summary>
        /// <param name="span">The URL path span to get the next segment from</param>
        /// <param name="remainder">The remaining path after extracting the segment</param>
        /// <returns>The next segment of the path before the first forward slash, or the entire path if no slash is found</returns>
        /// <example>
        /// For path "api/users/123":
        /// First call returns "api" with remainder "users/123"
        /// Second call returns "users" with remainder "123" 
        /// Third call returns "123" with remainder empty
        /// </example>
        public static ReadOnlySpan<char> GetNextSegment(this ReadOnlySpan<char> span, out ReadOnlySpan<char> remainder)
        {
            // Find the index of the first forward slash
            int slashIndex = span.IndexOf('/');

            // If no slash is found, return the entire span and set remainder to empty
            if (slashIndex == -1)
            {
                remainder = ReadOnlySpan<char>.Empty;
                return span;
            }

            // Set the remainder to everything after the slash
            remainder = span.Slice(slashIndex + 1);
            // Return everything before the slash
            return span.Slice(0, slashIndex);
        }
    }
}
