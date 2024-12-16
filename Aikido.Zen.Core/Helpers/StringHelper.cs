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
    }
}
