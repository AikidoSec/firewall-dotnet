using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;
using Aikido.Zen.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Aikido.Zen.Core.Helpers
{
    /// <summary>
    /// Helper class for processing HTTP request data and extracting it into a list of values and input locations.
    /// </summary>
    internal class HttpHelper
    {
        // Above ASP.NET Core's default (1024), so apps that raised their limit are still inspected.
        private const int MaxFormValueCount = 10_000;

        /// <summary>
        /// Represents the result of processing HTTP request data
        /// </summary>
        internal class HttpDataResult
        {
            /// <summary>
            /// The extracted request values and their dot-notation locations
            /// </summary>
            public List<UserInput> FlattenedData { get; set; }

            /// <summary>
            /// The parsed request body as a dictionary
            /// </summary>
            public object ParsedBody { get; set; }
        }

        /// <summary>
        /// Reads and flattens HTTP request data into a list of values and dot-notation locations.
        /// </summary>
        /// <param name="routeParams">The route parameters dictionary.</param>
        /// <param name="queryParams">The query parameters dictionary.</param>
        /// <param name="headers">The headers dictionary.</param>
        /// <param name="cookies">The cookies dictionary.</param>
        /// <param name="body">The request body stream.</param>
        /// <param name="contentType">The content type of the request.</param>
        /// <returns>A HttpDataResult containing both flattened data and parsed body.</returns>
        internal static async Task<HttpDataResult> ReadAndFlattenHttpDataAsync(
            IDictionary<string, string> routeParams,
            IDictionary<string, string> queryParams,
            IDictionary<string, string> headers,
            IDictionary<string, string> cookies,
            Stream body,
            string contentType,
            Encoding requestEncoding = null)
        {
            var result = new List<UserInput>();
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
                    parsedBody = await ProcessRequestBodyAsync(body, contentType, result, Encoding.UTF8);

                    // Apps may honor or ignore the charset they resolved from the header, so also inspect the body decoded with it.
                    // The encoding isn't used for multipart, so a second pass would only duplicate its inputs.
                    if (requestEncoding != null && requestEncoding.CodePage != Encoding.UTF8.CodePage && requestEncoding.CodePage != 65000 && body.CanSeek
                        && !ContentTypeHelper.TryGetMultipartBoundary(contentType, out _))
                    {
                        parsedBody = await ProcessRequestBodyAsync(body, contentType, result, requestEncoding) ?? parsedBody;
                    }
                }
            }
            catch (Exception e)
            {
                LogHelper.ErrorLog(Agent.Logger, $"caught error while parsing body: {e.Message}");
            }

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
        internal static string GetRawBody(Stream body)
        {
            // we read the stream, but leave it open so it can be read out later by http modules or middleware.
            // we try to detect the encdoding by looking for byte order marks at the beginning of the file, and use UTF-8 as a fallback.
            using (var reader = new StreamReader(body, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true, encoding: System.Text.Encoding.UTF8))
            {
                return reader.ReadToEndAsync().Result;
            }
        }

        private static async Task<object> ProcessRequestBodyAsync(Stream body, string contentType, List<UserInput> result, Encoding encoding)
        {
            object parsedBody = null;

            try
            {
                if (ContentTypeHelper.TryGetMultipartBoundary(contentType, out var boundary))
                {
                    parsedBody = await ProcessMultipartFormDataAsync(body, boundary, result);
                }
                else
                {
                    var mediaType = ContentTypeHelper.GetMediaType(contentType);

                    if (IsFormUrlEncodedMediaType(mediaType))
                    {
                        // Parse with our own reader instead of request.ReadFormAsync, so the app's request is not altered.
                        using (var formReader = new FormReader(body, encoding) { ValueCountLimit = MaxFormValueCount })
                        {
                            return FlattenFormFields(await formReader.ReadFormAsync(), result);
                        }
                    }

                    // we read the stream, but leave it open so it can be read out later by http modules or middleware.
                    using (var reader = new StreamReader(body, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true, encoding: encoding))
                    {
                        if (IsJsonMediaType(mediaType))
                        {
                            string jsonContent = await reader.ReadToEndAsync();
                            if (jsonContent.Length == 0)
                            {
                                return null;
                            }

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
                        else if (IsTextPlainMediaType(mediaType))
                        {
                            string textContent = await reader.ReadToEndAsync();
                            result.Add(new UserInput("body.textplain", textContent));
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

        private static Dictionary<string, object> FlattenFormFields(IEnumerable<KeyValuePair<string, StringValues>> formPairs, List<UserInput> result)
        {
            var formDict = new Dictionary<string, object>();
            foreach (var pair in formPairs)
            {
                var location = $"body.{pair.Key}";
                foreach (var value in pair.Value)
                {
                    result.Add(new UserInput(location, value));
                }

                var formValue = pair.Value.ToString();
                formDict[pair.Key] = formValue;

                if (pair.Value.Count > 1)
                {
                    result.Add(new UserInput(location, formValue));
                }
            }
            return formDict;
        }

        private static async Task<object> ProcessMultipartFormDataAsync(Stream body, string boundary, List<UserInput> result)
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
                    var sectionMediaType = ContentTypeHelper.GetMediaType(section.ContentType);

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
                        result.Add(new UserInput($"body.section.{sectionIndex}.file.{fileName}", fileContent));
                    }
                    else if (contentDisposition.IsFormDisposition())
                    {
                        var formField = contentDisposition.Name.Value;
                        var fieldValue = await section.ReadAsStringAsync();
                        formData[formField] = fieldValue;
                        result.Add(new UserInput($"body.section.{sectionIndex}.{formField}", fieldValue));
                    }
                }
                sectionIndex++;
            }

            return formData;
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
