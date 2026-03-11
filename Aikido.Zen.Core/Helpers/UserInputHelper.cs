using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Aikido.Zen.Core.Models;
using Microsoft.Net.Http.Headers;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// Helper class for processing user input paths.
    /// </summary>
    public static class UserInputHelper
    {
        private const int MaxDecodeUriPasses = 2;

        /// <summary>
        /// Processes all values and adds URI decoded variants where applicable (e.g. who%61mi => whoami).
        /// </summary>
        /// <param name="result">The dictionary to store processed data.</param>
        public static void ProcessUriValues(IDictionary<string, string> values)
        {
            if (values == null || values.Count == 0)
            {
                return;
            }

            foreach (var key in values.Keys.ToList())
            {
                string original = values[key];
                if (TryDecodeUriComponent(original, out string decoded))
                {
                    values[$"{key}|decoded"] = decoded;
                }
            }
        }

        /// <summary>
        /// Extracts the source of the user input from the flattened ParsedUserInput key.
        /// </summary>
        /// <param name="key">The key to analyze.</param>
        /// <returns>The source of the user input.</returns>
        public static Source GetAttackSourceFromUserInputKey(string key)
        {
            key = key.ToLower();

            if (key.StartsWith("query"))
            {
                return Source.Query;
            }
            else if (key.StartsWith("headers"))
            {
                return Source.Headers;
            }
            else if (key.StartsWith("cookies"))
            {
                return Source.Cookies;
            }
            else if (key.StartsWith("route"))
            {
                return Source.RouteParams;
            }
            return Source.Body;
        }

        public static string GetAttackPathFromUserInputKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            var normalizedPath = key;
            // ParsedUserInput may contain synthetic decoded variants such as "query.url|decoded".
            // The reported attack path should still point to the original field, e.g. ".url".
            var decodedSuffixIndex = normalizedPath.IndexOf('|');
            if (decodedSuffixIndex >= 0)
            {
                normalizedPath = normalizedPath.Substring(0, decodedSuffixIndex);
            }

            var segments = normalizedPath.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return string.Empty;
            }

            // Node reports the path relative to the source bucket, so "query.url" becomes ".url".
            var startIndex = IsSourceRoot(segments[0]) ? 1 : 0;
            if (startIndex >= segments.Length)
            {
                return ".";
            }

            var attackPath = string.Empty;
            for (var i = startIndex; i < segments.Length; i++)
            {
                // Flattened keys use ".0" for arrays, while attack paths use ".[0]".
                if (int.TryParse(segments[i], NumberStyles.None, CultureInfo.InvariantCulture, out var index))
                {
                    attackPath += $".[{index}]";
                }
                else
                {
                    attackPath += $".{segments[i]}";
                }
            }

            return attackPath;
        }

        /// <summary>
        /// Processes query parameters and adds them to the result dictionary.
        /// </summary>
        /// <param name="queryParams">The query parameters dictionary.</param>
        /// <param name="result">The dictionary to store processed data.</param>
        public static void ProcessQueryParameters(IDictionary<string, string> queryParams, IDictionary<string, string> result)
        {
            foreach (var query in queryParams)
            {
                result[$"query.{query.Key}"] = query.Value;
            }
        }

        /// <summary>
        /// Processes route parameters and adds them to the result dictionary.
        /// </summary>
        /// <param name="routeParams">The route parameters dictionary.</param>
        /// <param name="result">The dictionary to store processed data.</param>
        public static void ProcessRouteParameters(IDictionary<string, string> routeParams, IDictionary<string, string> result)
        {
            foreach (var routeParam in routeParams)
            {
                result[$"route.{routeParam.Key}"] = routeParam.Value;
            }
        }

        /// <summary>
        /// Processes headers and adds them to the result dictionary.
        /// </summary>
        /// <param name="headers">The headers dictionary.</param>
        /// <param name="result">The dictionary to store processed data.</param>
        public static void ProcessHeaders(IDictionary<string, string> headers, IDictionary<string, string> result)
        {
            foreach (var header in headers)
            {
                result[$"headers.{header.Key}"] = header.Value;
            }
        }

        /// <summary>
        /// Processes cookies and adds them to the result dictionary.
        /// </summary>
        /// <param name="cookies">The cookies dictionary.</param>
        /// <param name="result">The dictionary to store processed data.</param>
        public static void ProcessCookies(IDictionary<string, string> cookies, IDictionary<string, string> result)
        {
            foreach (var cookie in cookies)
            {
                result[$"cookies.{cookie.Key}"] = cookie.Value;
            }
        }

        /// <summary>
        /// Checks if the content type is multipart and extracts the boundary string.
        /// </summary>
        /// <param name="contentType">The content type to check.</param>
        /// <param name="boundary">The boundary string extracted from the content type.</param>
        /// <returns>True if the content type is multipart, false otherwise.</returns>
        public static bool IsMultipart(string contentType, out string boundary)
        {
            bool isMultipart = MediaTypeHeaderValue.TryParse(contentType, out var parsedContentType) && parsedContentType.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase);
            if (!isMultipart)
            {
                boundary = null;
                return false;
            }
            boundary = parsedContentType.Boundary.Value;
            return isMultipart;
        }

        private static bool TryDecodeUriComponent(string input, out string decoded)
        {
            bool changed = false;
            decoded = input;

            if (string.IsNullOrEmpty(input))
            {
                return false;
            }

            for (int i = 0; i < MaxDecodeUriPasses; i++)
            {
                string next = Uri.UnescapeDataString(decoded);
                if (next == decoded)
                {
                    break;
                }

                decoded = next;
                changed = true;
            }

            return changed;
        }

        private static bool IsSourceRoot(string segment)
        {
            switch (segment.ToLowerInvariant())
            {
                case "query":
                case "headers":
                case "cookies":
                case "route":
                case "body":
                case "graphql":
                case "xml":
                case "subdomains":
                    return true;
                default:
                    return false;
            }
        }
    }
}
