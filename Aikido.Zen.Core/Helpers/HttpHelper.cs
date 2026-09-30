using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

[assembly: InternalsVisibleTo("Aikido.Zen.Tests")]
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
        /// <param name="routeParams">The route parameters dictionary.</param>
        /// <param name="queryParams">The query parameters dictionary.</param>
        /// <param name="headers">The headers dictionary.</param>
        /// <param name="cookies">The cookies dictionary.</param>
        /// <param name="body">The request body stream.</param>
        /// <param name="contentType">The content type of the request.</param>
        /// <returns>A HttpDataResult containing both flattened data and parsed body.</returns>
        public static async Task<HttpDataResult> ReadAndFlattenHttpDataAsync(
            IDictionary<string, string> routeParams,
            IDictionary<string, string> queryParams,
            IDictionary<string, string> headers,
            IDictionary<string, string> cookies,
            Stream body,
            string contentType)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            object parsedBody = null;

            // Process Route Parameters
            UserInputHelper.ProcessRouteParameters(routeParams, result);

            // Process Query Parameters
            UserInputHelper.ProcessQueryParameters(queryParams, result);

            // Process Headers
            UserInputHelper.ProcessHeaders(headers, result);

            // Process Cookies
            UserInputHelper.ProcessCookies(cookies, result);

            // Process Body
            try
            {
                if (body != null && body.CanRead)
                {
                    parsedBody = await ProcessRequestBodyAsync(body, contentType, result);
                }
            }
            catch (Exception e)
            {
                LogHelper.ErrorLog(Agent.Logger, $"caught error while parsing body: {e.Message}");
            }

            // Add decoded URI strings to result dictionary where applicable
            UserInputHelper.ProcessUriValues(result);

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

        private static async Task<object> ProcessRequestBodyAsync(Stream body, string contentType, IDictionary<string, string> result)
        {
            object parsedBody = null;

            try
            {
                if (UserInputHelper.IsMultipart(contentType, out var boundary))
                {
                    parsedBody = await ProcessMultipartFormDataAsync(body, boundary, result);
                }
                else
                {
                    var mediaType = GetMediaType(contentType);

                    // we read the stream, but leave it open so it can be read out later by http modules or middleware.
                    // we try to detect the encdoding by looking for byte order marks at the beginning of the file, and use UTF-8 as a fallback.
                    using (var reader = new StreamReader(body, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true, encoding: System.Text.Encoding.UTF8))
                    {
                        if (IsJsonMediaType(mediaType))
                        {
                            string jsonContent = await reader.ReadToEndAsync();
                            using (JsonDocument document = JsonDocument.Parse(jsonContent))
                            {
                                JsonHelper.FlattenJson(result, document.RootElement, "body");
                                parsedBody = JsonHelper.ToJsonObj(document.RootElement);
                            }
                        }
                        else if (IsXmlMediaType(mediaType))
                        {
                            var xmlDoc = new XmlDocument();
                            using (var xmlReader = XmlReader.Create(reader, new XmlReaderSettings { Async = true, DtdProcessing = DtdProcessing.Ignore }))
                            {
                                xmlDoc.Load(xmlReader);
                                XmlHelper.FlattenXml(result, xmlDoc.DocumentElement, "body");
                                parsedBody = XmlHelper.XmlToObject(xmlDoc.DocumentElement);
                            }
                        }
                        else if (IsFormUrlEncodedMediaType(mediaType))
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
                        else if (IsTextPlainMediaType(mediaType))
                        {
                            string textContent = await reader.ReadToEndAsync();
                            result["body.textplain"] = textContent;
                            parsedBody = textContent;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (EnvironmentHelper.IsDebugging)
                {
                    LogHelper.ErrorLog(Agent.Logger, $"Error while parsing body: {e.Message}");
                }
            }
            finally
            {
                // reset the stream position
                if (body.CanSeek)
                {
                    body.Seek(0, SeekOrigin.Begin);
                }
            }

            return parsedBody;
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
                    var sectionMediaType = GetMediaType(section.ContentType);

                    if (IsJsonMediaType(sectionMediaType))
                    {
                        using (JsonDocument document = await JsonDocument.ParseAsync(section.Body))
                        {
                            JsonHelper.FlattenJson(result, document.RootElement, $"body.section.{sectionIndex}");
                            var jsonData = JsonHelper.ToJsonObj(document.RootElement);
                            if (contentDisposition.Name.HasValue)
                            {
                                formData[contentDisposition.Name.Value] = jsonData;
                            }
                        }
                    }
                    else if (IsXmlMediaType(sectionMediaType))
                    {
                        var xmlDoc = new XmlDocument();
                        using (var xmlReader = XmlReader.Create(section.Body, new XmlReaderSettings { Async = true, DtdProcessing = DtdProcessing.Ignore }))
                        {
                            xmlDoc.Load(xmlReader);
                        }
                        XmlHelper.FlattenXml(result, xmlDoc.DocumentElement, $"body.section.{sectionIndex}");
                        var xmlData = XmlHelper.XmlToObject(xmlDoc.DocumentElement);
                        if (contentDisposition.Name.HasValue)
                        {
                            formData[contentDisposition.Name.Value] = xmlData;
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

        /// <summary>
        /// Extracts the type/subtype of a Content-Type header. Matches ASP.NET Core's handling so we don't
        /// skip bodies sent with a header that MediaTypeHeaderValue rejects ("application/json _").
        /// </summary>
        internal static string GetMediaType(string contentType)
        {
            if (string.IsNullOrEmpty(contentType))
            {
                return string.Empty;
            }

            var index = 0;
            var length = contentType.Length;

            while (index < length && IsOptionalWhiteSpace(contentType[index]))
            {
                index++;
            }

            var typeStart = index;
            while (index < length && IsTokenChar(contentType[index]))
            {
                index++;
            }
            var typeLength = index - typeStart;

            while (index < length && IsOptionalWhiteSpace(contentType[index]))
            {
                index++;
            }

            if (typeLength == 0 || index >= length || contentType[index] != '/')
            {
                return string.Empty;
            }

            index++;

            while (index < length && IsOptionalWhiteSpace(contentType[index]))
            {
                index++;
            }

            var subTypeStart = index;
            while (index < length && IsTokenChar(contentType[index]))
            {
                index++;
            }
            var subTypeLength = index - subTypeStart;

            if (subTypeLength == 0)
            {
                return string.Empty;
            }

            // No whitespace around the '/': the pair is already contiguous.
            if (subTypeStart == typeStart + typeLength + 1)
            {
                return contentType.Substring(typeStart, typeLength + 1 + subTypeLength);
            }

            return string.Concat(
                contentType.Substring(typeStart, typeLength),
                "/",
                contentType.Substring(subTypeStart, subTypeLength));
        }

        // RFC 7230 token character. Anything else ends the type or subtype.
        private static bool IsTokenChar(char c)
        {
            if (c >= 'a' && c <= 'z') return true;
            if (c >= 'A' && c <= 'Z') return true;
            if (c >= '0' && c <= '9') return true;

            switch (c)
            {
                case '!':
                case '#':
                case '$':
                case '%':
                case '&':
                case '\'':
                case '*':
                case '+':
                case '-':
                case '.':
                case '^':
                case '_':
                case '`':
                case '|':
                case '~':
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsOptionalWhiteSpace(char c)
        {
            return c == ' ' || c == '\t';
        }

        private static bool IsJsonMediaType(string mediaType)
        {
            if (string.IsNullOrWhiteSpace(mediaType))
            {
                return false;
            }

            return mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
                   || mediaType.Equals("text/json", StringComparison.OrdinalIgnoreCase)
                   || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsXmlMediaType(string mediaType)
        {
            if (string.IsNullOrWhiteSpace(mediaType))
            {
                return false;
            }

            return mediaType.Equals("application/xml", StringComparison.OrdinalIgnoreCase)
                   || mediaType.Equals("text/xml", StringComparison.OrdinalIgnoreCase)
                   || mediaType.EndsWith("+xml", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsFormUrlEncodedMediaType(string mediaType)
        {
            if (string.IsNullOrWhiteSpace(mediaType))
            {
                return false;
            }

            return mediaType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTextPlainMediaType(string mediaType)
        {
            if (string.IsNullOrWhiteSpace(mediaType))
            {
                return false;
            }

            return mediaType.Equals("text/plain", StringComparison.OrdinalIgnoreCase);
        }

    }
}
