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
using System.Linq;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// Helper class for processing HTTP request data and extracting it into a flattened dictionary format.
    /// </summary>
    public class HttpHelper
    {
        /// <summary>
        /// Represents the result of processing HTTP request data
        /// </summary>
        public class HttpDataResult
        {
            /// <summary>
            /// The flattened request data with dot notation keys
            /// </summary>
            public IDictionary<string, string> FlattenedData { get; set; }

            /// <summary>
            /// The parsed request body as a dictionary
            /// </summary>
            public object ParsedBody { get; set; }
        }

        /// <summary>
        /// Reads and flattens HTTP request data into a dictionary with dot-notation keys.
        /// </summary>
        /// <param name="queryParams">The query parameters dictionary.</param>
        /// <param name="headers">The headers dictionary.</param>
        /// <param name="cookies">The cookies dictionary.</param>
        /// <param name="body">The request body stream.</param>
        /// <param name="contentType">The content type of the request.</param>
        /// <param name="contentLength">The content length of the request body.</param>
        /// <returns>A HttpDataResult containing both flattened data and parsed body.</returns>
        public static async Task<HttpDataResult> ReadAndFlattenHttpDataAsync(
            IDictionary<string, string> queryParams,
            IDictionary<string, string> headers,
            IDictionary<string, string> cookies,
            Stream body,
            string contentType,
            long contentLength,
            ILogger logger = null)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            object parsedBody = null;

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
                    parsedBody = await ProcessRequestBodyAsync(body, contentType, result);
                }
            }
            catch (Exception e)
            {
                logger?.LogError($"Aikido: caught error while parsing body: {e.Message}");
            }

            return new HttpDataResult
            {
                FlattenedData = result,
                ParsedBody = parsedBody
            };
        }

        /// <summary>
        /// Reads the raw body content from a stream.
        /// </summary>
        /// <param name="body"></param>
        /// <returns></returns>
        public static string GetRawBody(Stream body)
        {
            // we read the stream, but leave it open so it can be read out later by http modules or middleware.
            // we try to detect the encdoding by looking for byte order marks at the beginning of the file, and use UTF-8 as a fallback.
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

        private static async Task<object> ProcessRequestBodyAsync(Stream body, string contentType, IDictionary<string, string> result)
        {
            object parsedBody = null;

            try
            {
                if (IsMultipart(contentType, out var boundary))
                {
                    parsedBody = await ProcessMultipartFormDataAsync(body, boundary, result);
                }
                else
                {
                    // we read the stream, but leave it open so it can be read out later by http modules or middleware.
                    // we try to detect the encdoding by looking for byte order marks at the beginning of the file, and use UTF-8 as a fallback.
                    using (var reader = new StreamReader(body, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true, encoding: System.Text.Encoding.UTF8))
                    {
                        if (contentType.Contains("application/json"))
                        {
                            string jsonContent = await reader.ReadToEndAsync();
                            using (JsonDocument document = JsonDocument.Parse(jsonContent))
                            {
                                FlattenJson(result, document.RootElement, "body");
                                parsedBody = ToJsonObj(document.RootElement);
                            }
                        }
                        else if (contentType.Contains("application/xml") || contentType.Contains("text/xml"))
                        {
                            var xmlDoc = new XmlDocument();
                            xmlDoc.Load(reader);
                            FlattenXml(result, xmlDoc.DocumentElement, "body");
                            parsedBody = XmlToObject(xmlDoc.DocumentElement);
                        }
                        else if (contentType.Contains("application/x-www-form-urlencoded"))
                        {
                            string formString = await reader.ReadToEndAsync();
                            var formPairs = QueryHelpers.ParseQuery(formString);
                            var formDict = new Dictionary<string, object>();
                            foreach (var pair in formPairs)
                            {
                                result[$"body.{pair.Key}"] = pair.Value;
                                formDict[pair.Key] = pair.Value.ToString();
                            }
                            parsedBody = formDict;
                        }
                    }
                }
            }
            finally
            {
                // reset the stream position
                body.Seek(0, SeekOrigin.Begin);
            }

            return parsedBody;
        }

        private static object XmlToObject(XmlElement element)
        {
            // If element has no child elements, return its text value
            if (!element.HasChildNodes || (element.ChildNodes.Count == 1 && element.FirstChild is XmlText))
            {
                return element.InnerText.Trim();
            }

            // Create a dictionary to store child elements
            var dict = new Dictionary<string, object>();

            // Group child elements by name to detect arrays
            var childElementGroups = element.ChildNodes
                .OfType<XmlElement>()
                .GroupBy(x => x.Name)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var group in childElementGroups)
            {
                // If multiple elements with same name exist, create an array
                if (group.Value.Count > 1)
                {
                    dict[group.Key] = group.Value.Select(child => XmlToObject(child)).ToList();
                }
                else
                {
                    // Single element - add it directly to the dictionary
                    dict[group.Key] = XmlToObject(group.Value[0]);
                }
            }

            return dict;
        }

        private static async Task<object> ProcessMultipartFormDataAsync(Stream body, string boundary, IDictionary<string, string> result)
        {
            var reader = new MultipartReader(boundary, body);
            MultipartSection section = null;
            int sectionIndex = 0;

            // Create a dictionary to store all form data
            var formData = new Dictionary<string, object>();

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
                            var jsonData = ToJsonObj(document.RootElement);
                            if (contentDisposition.Name.HasValue)
                            {
                                formData[contentDisposition.Name.Value] = jsonData;
                            }
                        }
                    }
                    else if (section.ContentType == "application/xml" || section.ContentType == "text/xml")
                    {
                        using (var xmlReader = XmlReader.Create(section.Body, new XmlReaderSettings { Async = true, DtdProcessing = DtdProcessing.Ignore }))
                        {
                            var xmlDoc = new XmlDocument();
                            xmlDoc.Load(xmlReader);
                            FlattenXml(result, xmlDoc.DocumentElement, $"body.section.{sectionIndex}");
                            var xmlData = XmlToObject(xmlDoc.DocumentElement);
                            if (contentDisposition.Name.HasValue)
                            {
                                formData[contentDisposition.Name.Value] = xmlData;
                            }
                        }
                    }
                    else if (contentDisposition.IsFileDisposition())
                    {
                        var fileName = contentDisposition.FileName.Value;
                        var fileSize = section.Body.Length;
                        var fileContent = fileSize < 1024 * 1024 ? await section.ReadAsStringAsync() : $"File size: {fileSize / 1024 / 1024} MB";

                        var fileInfo = new Dictionary<string, object>
                        {
                            ["fileName"] = fileName,

                        };
                        if (contentDisposition.Name.HasValue)
                        {
                            formData[contentDisposition.Name.Value] = fileInfo;
                        }
                        result[$"body.section.{sectionIndex}.file.{fileName}"] = fileContent;
                    }
                    else if (contentDisposition.IsFormDisposition())
                    {
                        var formField = contentDisposition.Name.Value;
                        var fieldValue = await section.ReadAsStringAsync();
                        formData[formField] = fieldValue;
                        result[$"body.section.{sectionIndex}.{formField}"] = fieldValue;
                    }
                }
                sectionIndex++;
            }

            return formData;
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
            if (element.ValueKind == JsonValueKind.Array)
            {
                int index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    string arrayPrefix = string.IsNullOrEmpty(prefix) ? index.ToString() : $"{prefix}.{index}";
                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        FlattenJson(result, item, arrayPrefix);
                    }
                    else
                    {
                        result[arrayPrefix] = item.ToString();
                    }
                    index++;
                }
            }
            else if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    string key = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
                    if (property.Value.ValueKind == JsonValueKind.Object)
                    {
                        FlattenJson(result, property.Value, key);
                    }
                    else if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        FlattenJson(result, property.Value, key);
                    }
                    else
                    {
                        result[key] = property.Value.ToString();
                    }
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

        /// <summary>
        /// Converts a JsonElement to its appropriate native object representation
        /// </summary>
        /// <param name="element">The JsonElement to convert</param>
        /// <returns>The converted object</returns>
        private static object ToJsonObj(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var dict = new Dictionary<string, object>();
                    foreach (var property in element.EnumerateObject())
                    {
                        dict[property.Name] = ToJsonObj(property.Value);
                    }
                    return dict;

                case JsonValueKind.Array:
                    var list = new List<object>();
                    foreach (var item in element.EnumerateArray())
                    {
                        list.Add(ToJsonObj(item));
                    }
                    return list;

                case JsonValueKind.String:
                    return element.GetString();

                case JsonValueKind.Number:
                    // Handle numbers as double or long based on their representation
                    return element.TryGetInt64(out long l) ? l : element.GetDouble();

                case JsonValueKind.True:
                    return true;

                case JsonValueKind.False:
                    return false;

                case JsonValueKind.Null:
                    return null;

                default:
                    return null;
            }
        }
    }
}
