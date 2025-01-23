using System;
using System.Collections.Generic;
using System.Linq;

namespace Aikido.Zen.Core.Helpers.OpenAPI
{
    /// <summary>
    /// Helper class for determining string formats according to OpenAPI specification
    /// </summary>
    public static class StringFormatHelper
    {
        private static readonly HashSet<string> IndicationChars = new HashSet<string> { "-", ":", "@", ".", "://" };

        /// <summary>
        /// Get the format of a string according to OpenAPI specification
        /// </summary>
        /// <param name="str">The string to analyze</param>
        /// <returns>The OpenAPI format of the string, or null if no specific format is detected</returns>
        public static string GetStringFormat(string str)
        {
            // Skip if too short or too long
            if (str.Length < 5 || str.Length > 255)
                return null;

            var foundIndicationChars = CheckForIndicationChars(str);

            if (foundIndicationChars.Contains("-"))
            {
                if (foundIndicationChars.Contains(":"))
                {
                    if (IsDateTimeString(str))
                        return "date-time";
                }

                if (IsDateString(str))
                    return "date";

                if (IsUuidString(str))
                    return "uuid";
            }

            if (foundIndicationChars.Contains("@") && IsEmailString(str))
                return "email";

            if (foundIndicationChars.Contains("://") && IsUriString(str))
                return "uri";

            if (foundIndicationChars.Contains(".") && IsIPv4String(str))
                return "ipv4";

            if (foundIndicationChars.Contains(":") && IsIPv6String(str))
                return "ipv6";

            return null;
        }

        private static HashSet<string> CheckForIndicationChars(string str)
        {
            return new HashSet<string>(IndicationChars.Where(str.Contains));
        }

        private static bool IsDateTimeString(string str)
        {
            return DateTime.TryParse(str, out _);
        }

        private static bool IsDateString(string str)
        {
            return DateTime.TryParseExact(str, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out _);
        }

        private static bool IsUuidString(string str)
        {
            return Guid.TryParse(str, out _);
        }

        private static bool IsEmailString(string str)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(str);
                return addr.Address == str;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsUriString(string str)
        {
            return Uri.TryCreate(str, UriKind.Absolute, out _);
        }

        private static bool IsIPv4String(string str)
        {
            return System.Net.IPAddress.TryParse(str, out var ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
        }

        private static bool IsIPv6String(string str)
        {
            return System.Net.IPAddress.TryParse(str, out var ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;
        }
    }
}
