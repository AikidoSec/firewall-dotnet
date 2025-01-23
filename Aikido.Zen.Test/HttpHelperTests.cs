using Aikido.Zen.Core.Helpers;
using System.Text;
using System.Text.Json;

namespace Aikido.Zen.Test.Helpers
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
			IDictionary<string, string> expectedFlattenedData,
			object expectedParsedBody)
		{
			// Arrange
			using var bodyStream = new MemoryStream(Encoding.UTF8.GetBytes(body));

			// Act
			var result = await HttpHelper.ReadAndFlattenHttpDataAsync(queryParams, headers, cookies, bodyStream, contentType, bodyStream.Length);

			// Assert
			Assert.That(result.FlattenedData, Is.Not.Null);
			foreach (var expected in expectedFlattenedData)
			{
				Assert.That(result.FlattenedData[expected.Key], Is.EqualTo(expected.Value));
			}

			if (expectedParsedBody != null)
			{
				Assert.That(result.ParsedBody, Is.Not.Null);

				if (expectedParsedBody is IDictionary<string, object> expectedDict)
				{
					Assert.That(result.ParsedBody, Is.TypeOf<Dictionary<string, object>>());
					var actualDict = (Dictionary<string, object>)result.ParsedBody;
					foreach (var expected in expectedDict)
					{
						Assert.That(actualDict.ContainsKey(expected.Key), Is.True);
						Assert.That(actualDict[expected.Key]?.ToString(), Is.EqualTo(expected.Value?.ToString()));
					}
				}
				else if (expectedParsedBody is object[] expectedArray)
				{
					Assert.That(result.ParsedBody, Is.TypeOf<List<object>>());
					var actualArray = (List<object>)result.ParsedBody;
					Assert.That(actualArray.Count, Is.EqualTo(expectedArray.Length));
					for (int i = 0; i < expectedArray.Length; i++)
					{
						Assert.That(actualArray[i]?.ToString(), Is.EqualTo(expectedArray[i]?.ToString()));
					}
				}
				else
				{
                    var expectedParsedBodyJson = JsonSerializer.Serialize(expectedParsedBody);
                    var parsedBodyJson = JsonSerializer.Serialize(result.ParsedBody);
                    Assert.That(parsedBodyJson, Is.EqualTo(expectedParsedBodyJson));
				}
			}
			else
			{
				Assert.That(result.ParsedBody, Is.Null);
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
					testCase.ExpectedFlattenedData,
					testCase.ExpectedParsedBody
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
			public IDictionary<string, string> ExpectedFlattenedData { get; set; }
			public object ExpectedParsedBody { get; set; }
		}
	}
}
