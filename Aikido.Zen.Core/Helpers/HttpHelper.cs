using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using Aikido.Zen.Core.Models;
using Microsoft.Extensions.Logging;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// Helper class for processing HTTP request data and extracting it into a flattened dictionary format.
    /// </summary>
    public class HttpHelper
    {

        /// <summary>
        /// Reads and flattens HTTP request data into a dictionary with dot-notation keys.
        /// </summary>
        /// <param name="queryParams">The query parameters dictionary.</param>
        /// <param name="headers">The headers dictionary.</param>
        /// <param name="cookies">The cookies dictionary.</param>
        /// <param name="body">The request body stream.</param>
        /// <param name="contentType">The content type of the request.</param>
        /// <param name="contentLength">The content length of the request body.</param>
        /// <returns>A dictionary containing flattened request data with keys using dot notation.</returns>
        public static async Task<IDictionary<string, string>> ReadAndFlattenHttpDataAsync(
            IDictionary<string, string> queryParams,
            IDictionary<string, string> headers,
            IDictionary<string, string> cookies,
            Stream body,
            string contentType,
            long contentLength,
            ILogger logger = null)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Process Query Parameters
            ProcessQueryParameters(queryParams, result);

            // Process Headers
            ProcessHeaders(headers, result);

            // Process Cookies
            ProcessCookies(cookies, result);

            // Process Body
            try
            {
                if (contentLength > 0)
                {
                    await ProcessRequestBodyAsync(body, contentType, result);
                }
            }
            catch (Exception e)
            {
                logger?.LogError($"Aikido: caught error while parsing body: {e.Message}");
            }

            return result;
        }

        /// <summary>
        /// Reads the raw body content from a stream.
        /// </summary>
        /// <param name="body"></param>
        /// <returns></returns>
        public static string GetRawBody(Stream body)
        {
            using (var reader = new StreamReader(body, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true, encoding: System.Text.Encoding.UTF8))
            {
                return reader.ReadToEndAsync().Result;
            }
        }

        /// <summary>
        /// Extracts the source of the user input from the path.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
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

        // Processes query parameters and adds them to the result dictionary.
        private static void ProcessQueryParameters(IDictionary<string, string> queryParams, IDictionary<string, string> result)
        {
            foreach (var query in queryParams)
            {
                result[$"query.{query.Key}"] = query.Value;
            }
        }

        // Processes headers and adds them to the result dictionary.
        private static void ProcessHeaders(IDictionary<string, string> headers, IDictionary<string, string> result)
        {
            foreach (var header in headers)
            {
                result[$"headers.{header.Key}"] = header.Value;
            }
        }

        private static void ProcessCookies(IDictionary<string, string> cookies, IDictionary<string, string> result)
        {
            foreach (var cookie in cookies)
            {
                result[$"cookies.{cookie.Key}"] = cookie.Value;
            }
        }

        private static async Task ProcessRequestBodyAsync(Stream body, string contentType, IDictionary<string, string> result)
        {
            try
            {
                if (IsMultipart(contentType, out var boundary))
                {
                    await ProcessMultipartFormDataAsync(body, boundary, result);
                }
                else
                {
                    using (var reader = new StreamReader(body, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true, encoding: System.Text.Encoding.UTF8))
                    {
                        if (contentType.Contains("application/json"))
                        {
                            using (JsonDocument document = await JsonDocument.ParseAsync(body))
                            {
                                FlattenJson(result, document.RootElement, "body");
                            }

                        }
                        else if (contentType.Contains("application/xml") || contentType.Contains("text/xml"))
                        {
                            using (var xmlReader = XmlReader.Create(body, new XmlReaderSettings { Async = true }))
                            {
                                var xmlDoc = new XmlDocument();
                                xmlDoc.Load(xmlReader);
                                FlattenXml(result, xmlDoc.DocumentElement, "body");
                            }
                        }
                        else if (contentType.Contains("application/x-www-form-urlencoded"))
                        {
                            string formString = await reader.ReadToEndAsync();
                            var formPairs = QueryHelpers.ParseQuery(formString);
                            foreach (var pair in formPairs)
                            {
                                result[$"body.{pair.Key}"] = pair.Value;
                            }
                        }
                    }
                }
            }
            finally
            {
                // reset the stream position
                body.Seek(0, SeekOrigin.Begin);
            }
        }

        private static async Task ProcessMultipartFormDataAsync(Stream body, string boundary, IDictionary<string, string> result)
        {
            var reader = new MultipartReader(boundary, body);
            MultipartSection section = null;
            int sectionIndex = 0;

            while ((section = await reader.ReadNextSectionAsync()) != null)
            {
                var contentDisposition = section.GetContentDispositionHeader();
                if (contentDisposition != null)
                {
                    if (section.ContentType == "application/json")
                    {
                        using (JsonDocument document = await JsonDocument.ParseAsync(section.Body))
                        {
                            FlattenJson(result, document.RootElement, $"body.section.{sectionIndex}");
                        }
                    }
                    else if (section.ContentType == "application/xml" || section.ContentType == "text/xml")
                    {
                        using (var xmlReader = XmlReader.Create(section.Body, new XmlReaderSettings { Async = true }))
                        {
                            var xmlDoc = new XmlDocument();
                            xmlDoc.Load(xmlReader);
                            FlattenXml(result, xmlDoc.DocumentElement, $"body.section.{sectionIndex}");
                        }
                    }
                    else if (contentDisposition.IsFileDisposition())
                    {
                        var fileName = contentDisposition.FileName.Value;
                        var fileSize = section.Body.Length / 1024 / 1024;
                        var fileContent = fileSize < 1 ? await section.ReadAsStringAsync() : $"File size: {fileSize} MB";
                        result[$"body.section.{sectionIndex}.file.{fileName}"] = fileContent;
                    }
                    else if (contentDisposition.IsFormDisposition())
                    {
                        var formField = contentDisposition.Name.Value;
                        using (var formReader = new StreamReader(section.Body))
                        {
                            result[$"body.section.{sectionIndex}.{formField}"] = await section.ReadAsStringAsync();
                        }
                    }
                }
                sectionIndex++;
            }
        }

        // Checks if the content type is multipart and extracts the boundary string.
        private static bool IsMultipart(string contentType, out string boundary)
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

        private static void FlattenJson(IDictionary<string, string> result, JsonElement element, string prefix)
        {
            foreach (var property in element.EnumerateObject())
            {
                string key = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
                if (property.Value.ValueKind == JsonValueKind.Object)
                {
                    FlattenJson(result, property.Value, key);
                }
                else
                {
                    result[key] = property.Value.ToString();
                }
            }
        }

        private static void FlattenXml(IDictionary<string, string> result, XmlElement element, string prefix)
        {
            string newPrefix = string.IsNullOrEmpty(prefix) ? element.Name : $"{prefix}.{element.Name}";
            foreach (XmlNode childNode in element.ChildNodes)
            {
                if (childNode is XmlElement childElement)
                {
                    FlattenXml(result, childElement, newPrefix);
                }
                else if (childNode is XmlText textNode)
                {
                    result[newPrefix] = textNode.Value.Trim();
                }
            }
        }
    }
}
