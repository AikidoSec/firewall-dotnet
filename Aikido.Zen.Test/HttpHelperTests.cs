using Aikido.Zen.Core.Helpers;
using System.Text;
using System.Text.Json;

namespace Aikido.Zen.Test
{

	public class HttpHelperTests
	{
		[TestCaseSource(nameof(GetTestData))]
		public async Task ReadAndFlattenHttpDataAsync_ShouldProcessData(
			IDictionary<string, string> queryParams,
			IDictionary<string, string> headers,
			IDictionary<string, string> cookies,
			string body,
			string contentType,
			IDictionary<string, string> expectedResult)
		{
			// Arrange
			using var bodyStream = new MemoryStream(Encoding.UTF8.GetBytes(body));
			var formValues = new Dictionary<string, string>();
			var formFiles = new Dictionary<string, string>();

			// Act
			var result = await HttpHelper.ReadAndFlattenHttpDataAsync(queryParams, headers, cookies, bodyStream, contentType, bodyStream.Length);

			// Assert
			foreach (var expected in expectedResult)
			{
				Assert.AreEqual(expected.Value, result[expected.Key]);
			}
		}

		public static IEnumerable<TestCaseData> GetTestData()
		{
			var jsonData = File.ReadAllText("testdata/data.HttpHelper.json");
            var testCases = JsonSerializer.Deserialize<List<TestCase>>(jsonData, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

			foreach (var testCase in testCases)
			{
				yield return new TestCaseData(
					testCase.QueryParams,
					testCase.Headers,
					testCase.Cookies,
					testCase.Body,
					testCase.ContentType,
					testCase.ExpectedResult
				).SetName($"Test_{testCase.ContentType}_{testCases.IndexOf(testCase)}");
			}
		}

		private class TestCase
		{
			public IDictionary<string, string> QueryParams { get; set; }
			public IDictionary<string, string> Headers { get; set; }
			public IDictionary<string, string> Cookies { get; set; }
			public string Body { get; set; }
			public string ContentType { get; set; }
			public IDictionary<string, string> ExpectedResult { get; set; }
		}
	}
}
