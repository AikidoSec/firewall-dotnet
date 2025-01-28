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


            return null;
        }

        private static HashSet<string> CheckForIndicationChars(string str)
        {
            return new HashSet<string>(IndicationChars.Where(str.Contains));
        }

        private static bool IsDateTimeString(string str)
        {
            // Create a copy of the input string to avoid modifying the original
            string strToCheck = str;
            // Check for leap second format
            if (str.Contains("59:60"))
            {
                // Attempt to parse the string as a date-time without the leap second
                strToCheck = strToCheck.Replace("59:60", "59:59");
            }
            // Attempt to parse the string as a date-time using multiple formats
            string[] dateTimeFormats = {
                "yyyy-MM-ddTHH:mm:ssZ",
                "yyyy-MM-ddTHH:mm:ss.fffZ",
                "yyyy-MM-ddTHH:mm:ss.ffZ", // Added format to handle two fractional seconds
                "yyyy-MM-ddTHH:mm:ss.fZ",  // Added format to handle one fractional second
                "yyyy-MM-ddTHH:mm:sszzz",
                "yyyy-MM-ddTHH:mm:ss.fffzzz",
                "yyyy-MM-ddTHH:mm:ss.ffzzz", // Added format to handle two fractional seconds with timezone
                "yyyy-MM-ddTHH:mm:ss.fzzz",  // Added format to handle one fractional second with timezone
                "yyyy-MM-ddTHH:mm:ss'Z'", // Leap second format
                "yyyy-MM-ddTHH:mm:ss.fff'Z'", // Leap second format with milliseconds
                "yyyy-MM-ddTHH:mm:ss.ff'Z'", // Leap second format with two fractional seconds
                "yyyy-MM-ddTHH:mm:ss.f'Z'"  // Leap second format with one fractional second

            };

            return DateTime.TryParseExact(strToCheck, dateTimeFormats, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AllowWhiteSpaces, out _);
        }

        private static bool IsDateString(string str)
        {
            // Attempt to parse the string as a date using a simple date format
            string[] dateFormats = {
                "yyyy-MM-dd"
            };

            return DateTime.TryParseExact(str, dateFormats, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AllowWhiteSpaces, out _);
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
    }
}
