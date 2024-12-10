using System;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// Helper class for DateTime related operations.
    /// </summary>
    public class DateTimeHelper
    {
        /// <summary>
        /// Gets the current UTC time as Unix timestamp in milliseconds.
        /// </summary>
        /// <returns>The number of milliseconds that have elapsed since 1970-01-01T00:00:00Z.</returns>
        public static long UTCNowUnixMilliseconds()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}
