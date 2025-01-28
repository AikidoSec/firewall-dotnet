using System.Collections.Generic;

namespace Aikido.Zen.Core.Helpers.OpenAPI
{
    /// <summary>
    /// Helper class for determining API data types
    /// </summary>
    public static class ApiDataTypeHelper
    {
        /// <summary>
        /// Get the body data type based on content type header
        /// </summary>
        /// <param name="headers">Dictionary of HTTP headers</param>
        /// <returns>The data type as a string, or null if not determined</returns>
        public static string GetBodyDataType(IDictionary<string, string[]> headers)
        {
            if (!headers.TryGetValue("content-type", out var contentTypeValue) && !headers.TryGetValue("Content-Type", out contentTypeValue))
            {
                return null;
            }

                var contentType = string.Join(",", contentTypeValue).ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(contentType))
                return null;

            if (IsJsonContentType(contentType))
                return "json";

            if (contentType.StartsWith("application/x-www-form-urlencoded"))
                return "form-urlencoded";

            if (contentType.StartsWith("multipart/form-data"))
                return "form-data";

            if (contentType.Contains("xml"))
                return "xml";

            return null;
        }

        private static bool IsJsonContentType(string contentType)
        {
            return contentType.StartsWith("application/json")
                || contentType.Contains("+json")
                || contentType.StartsWith("application/csp-report")
                || contentType.StartsWith("application/x-json");
        }
    }
}
