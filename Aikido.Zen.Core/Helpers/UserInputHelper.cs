using System;
using System.Collections.Generic;
using System.Globalization;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// Helper class for processing user input paths.
    /// </summary>
    internal static class UserInputHelper
    {
        private const int MaxDecodeUriPasses = 2;

        /// <summary>
        /// Processes all values and adds URI decoded variants where applicable (e.g. who%61mi => whoami).
        /// </summary>
        /// <param name="values">The input values to process.</param>
        internal static void ProcessUriValues(List<UserInput> values)
        {
            if (values == null || values.Count == 0)
            {
                return;
            }

            var originalCount = values.Count;
            for (int i = 0; i < originalCount; i++)
            {
                var input = values[i];
                if (TryDecodeUriComponent(input.Value, out string decoded))
                {
                    values.Add(new UserInput(input.Location, decoded));
                }
            }
        }

        /// <summary>
        /// Extracts the source of the user input from the flattened ParsedUserInput key.
        /// </summary>
        /// <param name="key">The key to analyze.</param>
        /// <returns>The source of the user input.</returns>
        internal static Source GetAttackSourceFromUserInputKey(string key)
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

        internal static string GetAttackPathFromUserInputKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            var segments = key.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
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
        /// Processes query parameters and adds them to the result list.
        /// </summary>
        /// <param name="queryParams">The query parameters dictionary.</param>
        /// <param name="result">The list to store processed data.</param>
        internal static void ProcessQueryParameters(IDictionary<string, string> queryParams, List<UserInput> result)
        {
            foreach (var query in queryParams)
            {
                result.Add(new UserInput($"query.{query.Key}", query.Value));
            }
        }

        /// <summary>
        /// Processes route parameters and adds them to the result list.
        /// </summary>
        /// <param name="routeParams">The route parameters dictionary.</param>
        /// <param name="result">The list to store processed data.</param>
        internal static void ProcessRouteParameters(IDictionary<string, string> routeParams, List<UserInput> result)
        {
            foreach (var routeParam in routeParams)
            {
                result.Add(new UserInput($"route.{routeParam.Key}", routeParam.Value));
            }
        }

        /// <summary>
        /// Processes headers and adds them to the result list.
        /// </summary>
        /// <param name="headers">The headers dictionary.</param>
        /// <param name="result">The list to store processed data.</param>
        internal static void ProcessHeaders(IDictionary<string, string> headers, List<UserInput> result)
        {
            foreach (var header in headers)
            {
                result.Add(new UserInput($"headers.{header.Key}", header.Value));
            }
        }

        /// <summary>
        /// Processes cookies and adds them to the result list.
        /// </summary>
        /// <param name="cookies">The cookies dictionary.</param>
        /// <param name="result">The list to store processed data.</param>
        internal static void ProcessCookies(IDictionary<string, string> cookies, List<UserInput> result)
        {
            foreach (var cookie in cookies)
            {
                result.Add(new UserInput($"cookies.{cookie.Key}", cookie.Value));
            }
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
