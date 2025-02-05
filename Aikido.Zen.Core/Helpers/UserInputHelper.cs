using System;
using System.Collections.Generic;
using Aikido.Zen.Core.Models;
using Microsoft.Net.Http.Headers;
using System.Text.Json;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// Helper class for processing user input paths.
    /// </summary>
    public static class UserInputHelper
    {
        /// <summary>
        /// Extracts the source of the user input from the path.
        /// </summary>
        /// <param name="path">The path to analyze.</param>
        /// <returns>The source of the user input.</returns>
        public static Source GetSourceFromUserInputPath(string path)
        {
            path = path.ToLower();

            if (path.StartsWith("query"))
            {
                return Source.Query;
            }
            else if (path.StartsWith("headers"))
            {
                return Source.Headers;
            }
            else if (path.StartsWith("cookies"))
            {
                return Source.Cookies;
            }
            else if (path.StartsWith("route"))
            {
                return Source.RouteParams;
            }
            return Source.Body;
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
                if (JsonHelper.TryParseJson(query.Value, out var jsonValue))
                {
                    JsonHelper.FlattenJson(result, jsonValue, $"query.{query.Key}");
                }
                else
                {
                    result[$"query.{query.Key}"] = query.Value;
                }
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
                if (JsonHelper.TryParseJson(header.Value, out var jsonValue))
                {
                    JsonHelper.FlattenJson(result, jsonValue, $"headers.{header.Key}");
                }
                else
                {
                    result[$"headers.{header.Key}"] = header.Value;
                }
            }

            if (headers.TryGetValue("Authorization", out var authHeader))
            {
                var (isJwt, decodedObject) = JwtHelper.TryDecodeAsJwt(authHeader);
                if (isJwt && decodedObject != null && !result.ContainsKey("auth"))
                {
                    JsonHelper.FlattenJson(result, JsonDocument.Parse(JsonSerializer.Serialize(decodedObject)).RootElement, "auth.jwt");
                }
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
    }
}
