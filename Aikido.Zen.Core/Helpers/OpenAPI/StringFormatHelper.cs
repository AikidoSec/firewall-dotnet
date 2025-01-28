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

        /// <summary>
        /// Checks if the string is a date-time according to RFC3339.
        /// </summary>
        /// <param name="str">The string to analyze.</param>
        /// <returns>True if the string is a valid RFC3339 date-time, false otherwise.</returns>
        private static bool IsDateTimeString(string str)
        {
            // Define the regex pattern for RFC3339 date-time format
            var pattern = new System.Text.RegularExpressions.Regex(
                @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Check if the string length is within the valid range
            if (str.Length < 20 || str.Length > 29)
            {
                return false;
            }

            // Validate the string against the regex pattern
            if (!pattern.IsMatch(str))
            {
                return false;
            }

            // Split the string into date and time components
            var parts = str.Split('T');
            var dateParts = parts[0].Split('-');
            var timeParts = parts[1].Split(':');

            // Validate date values
            int year = int.Parse(dateParts[0]);
            int month = int.Parse(dateParts[1]);
            int day = int.Parse(dateParts[2]);
            if (month < 1 || month > 12 || day < 1 || day > 31 || year < 0)
            {
                return false;
            }

            // Validate time values
            int hour = int.Parse(timeParts[0]);
            int minute = int.Parse(timeParts[1]);
            int second = int.Parse(timeParts[2].Substring(0, 2)); // Extract seconds part
            if (hour < 0 || hour > 23 || minute < 0 || minute > 59 || second < 0 || second > 60)
            {
                return false;
            }

            // Validate time offset if present
            if (!str.EndsWith("Z"))
            {
                var offset = str.Substring(str.Length - 5);
                var offsetParts = offset.Split(':');
                int offsetHour = int.Parse(offsetParts[0]);
                int offsetMinute = int.Parse(offsetParts[1]);
                if (offsetHour < 0 || offsetHour > 23 || offsetMinute < 0 || offsetMinute > 60)
                {
                    return false;
                }
            }

            return true;
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
