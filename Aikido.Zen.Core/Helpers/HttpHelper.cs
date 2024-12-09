using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using System.Linq;

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
            long contentLength)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // Case-insensitive keys

            // Process Query Parameters
            ProcessQueryParameters(queryParams, result);

            // Process Headers
            ProcessHeaders(headers, result);

            // Process Cookies
            ProcessCookies(cookies, result);

            // Process Body
            if (contentLength > 0)
            {
                await ProcessRequestBodyAsync(body, contentType, result);
            }

            return result;
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

        // Processes cookies and adds them to the result dictionary.
        private static void ProcessCookies(IDictionary<string, string> cookies, IDictionary<string, string> result)
        {
            foreach (var cookie in cookies)
            {
                result[$"cookies.{cookie.Key}"] = cookie.Value;
            }
        }

        // Processes the request body based on its content type and adds the data to the result dictionary.
        private static async Task ProcessRequestBodyAsync(Stream body, string contentType, IDictionary<string, string> result)
        {
            try
            {
                if (IsMultipart(contentType, out string boundary))
                {
                    await ProcessMultipartFormDataAsync(body, boundary, result);
                }
                else
                {
                    using (var reader = new StreamReader(body, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true, encoding: System.Text.Encoding.UTF8))
                    {
                        if (contentType.Contains("application/json"))
                        {
                            string json = await reader.ReadToEndAsync();
                            try {
                                var jsonData = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                                Flatten(result, jsonData, "body");
                            }
                            catch (JsonException)
                            {
                                result["body.text"] = json;
                            }
                        }
                        else if (contentType.Contains("application/xml") || contentType.Contains("text/xml"))
                        {
                            string xmlString = await reader.ReadToEndAsync();
                            var xmlDoc = new XmlDocument();
                            try
                            {
                                xmlDoc.LoadXml(xmlString);
                                FlattenXml(result, xmlDoc.DocumentElement, "body");

                            }
                            catch (Exception)
                            {
                                result["body.text"] = xmlString;
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
                        else
                        {
                            string text = await reader.ReadToEndAsync();
                            result["body.text"] = text;
                        }
                    }
                }
            }
            finally
            {
                body.Seek(0, SeekOrigin.Begin); // Reset the stream position
            }
        }

        // Processes multipart form-data and adds the data to the result dictionary.
        private static async Task ProcessMultipartFormDataAsync(Stream body, string boundary, IDictionary<string, string> result)
        {
            var reader = new MultipartReader(boundary, body);
            MultipartSection currentSection = null;
            var currentSectionIndex = 0;
            do
            {
                currentSection = await reader.ReadNextSectionAsync();
                if (currentSection != null)
                {
                    var contentDisposition = currentSection.GetContentDispositionHeader();
                    if (contentDisposition == null)
                        continue;
                    if (currentSection.ContentType == "application/json")
                    {
                        var json = await currentSection.ReadAsStringAsync();
                        try
                        {
                            var jsonData = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                            Flatten(result, jsonData, "body.section." + currentSectionIndex);
                        }
                        catch (Exception)
                        {
                            result[$"body.section.{currentSectionIndex}.text"] = json;
                        }
                    }
                    else if (currentSection.ContentType == "application/xml" || currentSection.ContentType == "text/xml")
                    {
                        var xmlString = await currentSection.ReadAsStringAsync();
                        var xmlDoc = new XmlDocument();
                        try
                        {
                            xmlDoc.LoadXml(xmlString);
                            FlattenXml(result, xmlDoc.DocumentElement, "body.section." + currentSectionIndex);
                        }
                        catch (Exception)
                        {
                            result[$"body.section.{currentSectionIndex}.text"] = xmlString;
                        }
                    }
                    else if (contentDisposition.IsFileDisposition())
                    {
                        var fileName = contentDisposition.FileName.Value;
                        var fileSize = currentSection.Body.Length / 1024 / 1024;
                        var fileContent = fileSize < 1 ? await currentSection.ReadAsStringAsync() : $"File size: {fileSize} MB";
                        result[$"body.section.{currentSectionIndex}.file.{fileName}"] = fileContent;
                    }
                    else if (contentDisposition.IsFormDisposition())
                    {
                        var formField = contentDisposition.Name.Value;
                        var formContent = await currentSection.ReadAsStringAsync();
                        result[$"body.section.{currentSectionIndex}.{formField}"] = formContent;
                    }
                    else
                    {
                        result[$"body.section.{currentSectionIndex}.text"] = await currentSection.ReadAsStringAsync();
                    }
                }
            } while (currentSection != null);
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

        // Recursively flattens a nested dictionary into a single-level dictionary using dot notation for keys.
        private static void Flatten(IDictionary<string, string> result, IDictionary<string, object> data, string prefix)
        {
            foreach (var kvp in data)
            {
                string key = string.IsNullOrEmpty(prefix) ? kvp.Key : $"{prefix}.{kvp.Key}";

                if (kvp.Value is IDictionary<string, object> dicValue)
                {
                    Flatten(result, dicValue, key);
                }
                else
                {
                    result[key] = kvp.Value?.ToString() ?? string.Empty;
                }
            }
        }

        // // Recursively flattens an XML element into a dictionary using dot notation for keys.
        private static void FlattenXml(IDictionary<string, string> result, XmlElement element, string prefix)
        {
            string newPrefix = string.IsNullOrEmpty(prefix) ? element.Name : $"{prefix}.{element.Name}";

            // Keep track of how many times we've seen each element name
            var elementCounts = new Dictionary<string, int>();

            foreach (XmlNode childNode in element.ChildNodes)
            {
                if (childNode is XmlElement childElement)
                {
                    // For repeated elements, append an index, we do this by checking if there is more than one child element with the same name
                    if (!elementCounts.ContainsKey(childElement.Name))
                    {
                        elementCounts[childElement.Name] = 0;
                    }
                    string indexedPrefix = $"{newPrefix}.{childElement.Name}";
                    if (element.ChildNodes.Cast<XmlNode>().Count(n => n.Name == childElement.Name) > 1)
                        indexedPrefix += $".{elementCounts[childElement.Name]}";
                    elementCounts[childElement.Name]++;

                    if (childElement.ChildNodes.Count == 1 && childElement.FirstChild is XmlText textNode)
                    {
                        // If element has only text content, add it directly
                        result[indexedPrefix] = textNode.Value.Trim();
                    }
                    else
                    {
                        // If element has child elements, recurse
                        FlattenXml(result, childElement, indexedPrefix);
                    }

                    // Process attributes
                    foreach (XmlAttribute attribute in childElement.Attributes)
                    {
                        result[$"{indexedPrefix}.{attribute.Name}"] = attribute.Value.Trim();
                    }
                }
                else if (childNode is XmlText textNode && !string.IsNullOrWhiteSpace(textNode.Value))
                {
                    // For text nodes, add or update the value directly
                    if (result.ContainsKey(newPrefix) && result[newPrefix] != textNode.Value.Trim())
                    {
                        result[newPrefix] = textNode.Value.Trim();
                    }
                    else
                    {
                        result[newPrefix] = textNode.Value.Trim();
                    }
                }
            }
        }
    }
}
