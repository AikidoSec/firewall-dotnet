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
    [SimpleJob(RuntimeMoniker.Net48, baseline: false, warmupCount: 1, iterationCount: 2)]
    [SimpleJob(RuntimeMoniker.Net80, baseline: true, warmupCount: 1, iterationCount: 2)]
    [HideColumns(Column.StdErr, Column.StdDev, Column.Error, Column.Min, Column.Max, Column.RatioSD)]
    public class HttpHelperBenchmarks
    {
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
        private string MultipartFormContentType => $"multipart/form-data; boundary={_boundary}";

        [Params(1, 10, 100, 1000)] // Test different sizes
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

        private string CreateMultipartFormDataContentWithDummyFile(int size)
        {
            var sb = new StringBuilder();

            // Add form fields
            for (int i = 1; i <= size; i++)
            {
                sb.AppendLine($"--{_boundary}");
                sb.AppendLine($"Content-Disposition: form-data; name=\"key{i}\"");
                sb.AppendLine();
                sb.AppendLine($"value{i}");
            }

            // Add dummy file
            sb.AppendLine($"--{_boundary}");
            sb.AppendLine($"Content-Disposition: form-data; name=\"file\"; filename=\"dummy.txt\"");
            sb.AppendLine("Content-Type: text/plain");
            sb.AppendLine();
            sb.AppendLine(CreateLargeDummyFileContent(size));

            // Add final boundary
            sb.AppendLine($"--{_boundary}--");

            return sb.ToString();
        }

        private string CreateLargeDummyFileContent(int size)
        {
            // Create a dummy file with size / 10 MB
            int mb = size * 1024 * 1024 / 10;
            return new string('a', mb);
        }

        [GlobalSetup]
        public void Setup()
        {
            _boundary = Guid.NewGuid().ToString();

            _queryParams = new Dictionary<string, string>
            {
                { "param1", "value1" },
                { "param2", "value2" }
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
            var multipartContent = CreateMultipartFormDataContentWithDummyFile(PayloadSize);

            _jsonBody = new MemoryStream(Encoding.UTF8.GetBytes(jsonContent));
            _xmlBody = new MemoryStream(Encoding.UTF8.GetBytes(xmlContent));
            _formBody = new MemoryStream(Encoding.UTF8.GetBytes(formContent));
            _multipartFormBody = new MemoryStream(Encoding.UTF8.GetBytes(multipartContent));
        }

        [Benchmark]
        public async Task ProcessJsonRequest()
        {
            var result = await HttpHelper.ReadAndFlattenHttpDataAsync(
                "/request/path",
                "/request/{route}",
                _queryParams,
                _headers,
                _cookies,
                _jsonBody,
                JsonContentType,
                _jsonBody.Length
            );
            _jsonBody.Position = 0;
        }

        [Benchmark]
        public async Task ProcessXmlRequest()
        {
            var result = await HttpHelper.ReadAndFlattenHttpDataAsync(
                "/request/path",
                "/request/{route}",
                _queryParams,
                _headers,
                _cookies,
                _xmlBody,
                XmlContentType,
                _xmlBody.Length
            );
            _xmlBody.Position = 0;
        }

        [Benchmark]
        public async Task ProcessFormRequest()
        {
            var result = await HttpHelper.ReadAndFlattenHttpDataAsync(
                "/request/path",
                "/request/{route}",
                _queryParams,
                _headers,
                _cookies,
                _formBody,
                FormContentType,
                _formBody.Length
            );
            _formBody.Position = 0;
        }

        [Benchmark]
        public async Task ProcessMultipartFormDataRequest()
        {
            var result = await HttpHelper.ReadAndFlattenHttpDataAsync(
                "/request/path",
                "/request/{route}",
                _queryParams,
                _headers,
                _cookies,
                _multipartFormBody,
                MultipartFormContentType,
                _multipartFormBody.Length
            );
            _multipartFormBody.Position = 0;
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
