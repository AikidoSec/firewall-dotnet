using System;

namespace Aikido.Zen.Core.Helpers
{
    public class DateTimeHelper
    {
        public static long UTCNowUnixMilliseconds()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}
