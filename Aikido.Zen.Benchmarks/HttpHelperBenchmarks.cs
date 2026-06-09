using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Aikido.Zen.Core.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Text;
using System.Linq;
using BenchmarkDotNet.Columns;

namespace Aikido.Zen.Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net10_0, baseline: true)]
    [MinIterationTime(100)]
    [Outliers(Perfolizer.Mathematics.OutlierDetection.OutlierMode.RemoveAll)]
    [HideColumns(Column.StdErr, Column.StdDev, Column.Error, Column.Min, Column.Max, Column.RatioSD)]
    public class HttpHelperBenchmarks
    {
        private IDictionary<string, string> _routeParams;
        private IDictionary<string, string> _queryParams;
        private IDictionary<string, string> _headers;
        private IDictionary<string, string> _cookies;
        private Stream _jsonBody;
        private Stream _xmlBody;
        private Stream _formBody;
        private Stream _multipartFormBody;

        private string _boundary;

        private const string JsonContentType = "application/json";
        private const string XmlContentType = "application/xml";
        private const string FormContentType = "application/x-www-form-urlencoded";
        private const int LargeMultipartPayloadSize = 1000;
        private const int MultipartFileSizeBytes = 10 * 1024 * 1024;
        private string MultipartFileResultKey => $"body.section.{PayloadSize}.file.dummy.txt";
        private string MultipartFormContentType => $"multipart/form-data; boundary={_boundary}";

        [Params(10, 1000)]
        public int PayloadSize { get; set; }

        private string CreateJsonContent(int size)
        {
            var items = Enumerable.Range(1, size)
                .Select(i => $"\"key{i}\":\"value{i}\"");
            return "{" + string.Join(",", items) + "}";
        }

        private string CreateXmlContent(int size)
        {
            var items = Enumerable.Range(1, size)
                .Select(i => $"<key{i}>value{i}</key{i}>");
            return "<root>" + string.Join("", items) + "</root>";
        }

        private string CreateFormContent(int size)
        {
            var items = Enumerable.Range(1, size)
                .Select(i => $"key{i}=value{i}");
            return string.Join("&", items);
        }

        private string CreateMultipartFormDataContent(int size)
        {
            var sb = new StringBuilder();

            for (int i = 1; i <= size; i++)
            {
                sb.Append("--").Append(_boundary).Append("\r\n");
                sb.Append("Content-Disposition: form-data; name=\"key").Append(i).Append("\"").Append("\r\n\r\n");
                sb.Append("value").Append(i).Append("\r\n");
            }

            if (size >= LargeMultipartPayloadSize)
            {
                sb.Append("--").Append(_boundary).Append("\r\n");
                sb.Append("Content-Disposition: form-data; name=\"file\"; filename=\"dummy.txt\"").Append("\r\n");
                sb.Append("Content-Type: text/plain").Append("\r\n\r\n");
                sb.Append('a', MultipartFileSizeBytes).Append("\r\n");
            }

            sb.Append("--").Append(_boundary).Append("--").Append("\r\n");

            return sb.ToString();
        }

        [GlobalSetup]
        public void Setup()
        {
            _boundary = "benchmark-boundary";

            _queryParams = new Dictionary<string, string>
            {
                { "param1", "value1" },
                { "param2", "value2" }
            };

            _routeParams = new Dictionary<string, string>
            {
                { "routeParam1", "value1" },
                { "routeParam2", "value2" }
            };

            _headers = new Dictionary<string, string>
            {
                { "header1", "value1" },
                { "header2", "value2" }
            };

            _cookies = new Dictionary<string, string>
            {
                { "cookie1", "value1" },
                { "cookie2", "value2" }
            };

            var jsonContent = CreateJsonContent(PayloadSize);
            var xmlContent = CreateXmlContent(PayloadSize);
            var formContent = CreateFormContent(PayloadSize);
            var multipartContent = CreateMultipartFormDataContent(PayloadSize);

            _jsonBody = new MemoryStream(Encoding.UTF8.GetBytes(jsonContent));
            _xmlBody = new MemoryStream(Encoding.UTF8.GetBytes(xmlContent));
            _formBody = new MemoryStream(Encoding.UTF8.GetBytes(formContent));
            _multipartFormBody = new MemoryStream(Encoding.UTF8.GetBytes(multipartContent));
        }

        [Benchmark]
        public async Task<int> ProcessJsonRequest()
        {
            var result = await HttpHelper.ReadAndFlattenHttpDataAsync(
                _routeParams,
                _queryParams,
                _headers,
                _cookies,
                _jsonBody,
                JsonContentType
            );
            _jsonBody.Position = 0;
            return result.FlattenedData.Count;
        }

        [Benchmark]
        public async Task<int> ProcessXmlRequest()
        {
            var result = await HttpHelper.ReadAndFlattenHttpDataAsync(
                _routeParams,
                _queryParams,
                _headers,
                _cookies,
                _xmlBody,
                XmlContentType
            );
            _xmlBody.Position = 0;
            return result.FlattenedData.Count;
        }

        [Benchmark]
        public async Task<int> ProcessFormRequest()
        {
            var result = await HttpHelper.ReadAndFlattenHttpDataAsync(
                _routeParams,
                _queryParams,
                _headers,
                _cookies,
                _formBody,
                FormContentType
            );
            _formBody.Position = 0;
            return result.FlattenedData.Count;
        }

        [Benchmark]
        public async Task<int> ProcessMultipartFormDataRequest()
        {
            var result = await HttpHelper.ReadAndFlattenHttpDataAsync(
                _routeParams,
                _queryParams,
                _headers,
                _cookies,
                _multipartFormBody,
                MultipartFormContentType
            );
            _multipartFormBody.Position = 0;

            if (PayloadSize >= LargeMultipartPayloadSize &&
                !result.FlattenedData.TryGetValue(MultipartFileResultKey, out _))
            {
                throw new InvalidOperationException("The multipart file section was not parsed.");
            }

            return result.FlattenedData.Count;
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _jsonBody?.Dispose();
            _xmlBody?.Dispose();
            _formBody?.Dispose();
            _multipartFormBody?.Dispose();
        }

    }
}
