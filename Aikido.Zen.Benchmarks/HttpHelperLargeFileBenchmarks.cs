using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Jobs;
using Aikido.Zen.Core.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Aikido.Zen.Benchmarks
{
    [SimpleJob(RuntimeMoniker.Net10_0, baseline: true)]
    [MinIterationTime(100)]
    [Outliers(Perfolizer.Mathematics.OutlierDetection.OutlierMode.RemoveAll)]
    [HideColumns(Column.StdErr, Column.StdDev, Column.Error, Column.Min, Column.Max, Column.RatioSD)]
    public class HttpHelperLargeFileBenchmarks
    {
        private const string Boundary = "benchmark-boundary";
        private const int FileSizeMegabytes = 10;
        private const string FileResultKey = "body.section.1.file.dummy.txt";

        private IDictionary<string, string> _routeParams;
        private IDictionary<string, string> _queryParams;
        private IDictionary<string, string> _headers;
        private IDictionary<string, string> _cookies;
        private Stream _multipartFormBody;

        private string MultipartFormContentType => $"multipart/form-data; boundary={Boundary}";

        [GlobalSetup]
        public void Setup()
        {
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

            _multipartFormBody = new MemoryStream(Encoding.UTF8.GetBytes(CreateMultipartFormDataContent()));
        }

        private string CreateMultipartFormDataContent()
        {
            var builder = new StringBuilder();
            builder.Append("--").Append(Boundary).Append("\r\n");
            builder.Append("Content-Disposition: form-data; name=\"metadata\"").Append("\r\n\r\n");
            builder.Append("benchmark").Append("\r\n");
            builder.Append("--").Append(Boundary).Append("\r\n");
            builder.Append("Content-Disposition: form-data; name=\"file\"; filename=\"dummy.txt\"").Append("\r\n");
            builder.Append("Content-Type: text/plain").Append("\r\n\r\n");
            builder.Append('a', FileSizeMegabytes * 1024 * 1024).Append("\r\n");
            builder.Append("--").Append(Boundary).Append("--").Append("\r\n");
            return builder.ToString();
        }

        [Benchmark]
        public async Task<int> ProcessMultipartFormDataRequestWithLargeFile()
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

            if (!result.FlattenedData.TryGetValue(FileResultKey, out var fileResult))
            {
                throw new InvalidOperationException("The multipart file section was not parsed.");
            }

            return result.FlattenedData.Count + fileResult.Length;
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _multipartFormBody?.Dispose();
        }
    }
}
