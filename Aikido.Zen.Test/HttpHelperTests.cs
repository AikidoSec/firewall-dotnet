using Aikido.Zen.Core;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;
using System.Text.Json;
using System.Xml;

namespace Aikido.Zen.Test.Helpers
{

    public class HttpHelperTests
    {
        [TestCaseSource(nameof(GetTestData))]
        public async Task ReadAndFlattenHttpDataAsync_ShouldProcessData(
            IDictionary<string, string> routeParams,
            IDictionary<string, string> queryParams,
            IDictionary<string, string> headers,
            IDictionary<string, string> cookies,
            string body,
            string contentType,
            List<UserInput> expectedFlattenedData,
            object expectedParsedBody)
        {
            // Arrange
            using var bodyStream = new MemoryStream(Encoding.UTF8.GetBytes(body));

            // Act
            var result = await HttpHelper.ReadAndFlattenHttpDataAsync(routeParams, queryParams, headers, cookies, bodyStream, contentType);

            // Assert
            Assert.That(result.FlattenedData, Is.Not.Null);
            Assert.That(result.FlattenedData, Is.EquivalentTo(expectedFlattenedData));

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
                    testCase.RouteParams,
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
            public IDictionary<string, string>? RouteParams { get; set; }
            public IDictionary<string, string>? QueryParams { get; set; }
            public IDictionary<string, string>? Headers { get; set; }
            public IDictionary<string, string>? Cookies { get; set; }
            public string? Body { get; set; }
            public string? ContentType { get; set; }
            public List<UserInput>? ExpectedFlattenedData { get; set; }
            public object? ExpectedParsedBody { get; set; }
        }

        [Test]
        public async Task ReadAndFlattenHttpDataAsync_ShouldDecodePercentEncodedUserInputValues()
        {
            // Arrange
            var routeParams = new Dictionary<string, string> { { "command", "who%61mi" } };
            var queryParams = new Dictionary<string, string> {
                { "path", "%2e%2e%2fetc%2fpasswd" },
                { "emoji", "%F0%9F%98%80" },
                { "double", "%2577%2568%256f%2561%256d%2569" },
                { "invalid", "%E0%A4%A" }
            };
            var headers = new Dictionary<string, string> { { "X-Custom", "a+b%2Bc" } };
            var cookies = new Dictionary<string, string> { { "session", "abc%31%32%33" } };
            const string body = "{\"cmd\":\"who%61mi\",\"literal\":\"a+b\"}";
            using var bodyStream = new MemoryStream(Encoding.UTF8.GetBytes(body));

            // Act
            var result = await HttpHelper.ReadAndFlattenHttpDataAsync(
                routeParams,
                queryParams,
                headers,
                cookies,
                bodyStream,
                "application/json");

            // Assert
            Assert.That(result.FlattenedData, Is.EquivalentTo(new[]
            {
                new UserInput("route.command", "who%61mi"),
                new UserInput("query.path", "%2e%2e%2fetc%2fpasswd"),
                new UserInput("query.emoji", "%F0%9F%98%80"),
                new UserInput("query.double", "%2577%2568%256f%2561%256d%2569"),
                new UserInput("query.invalid", "%E0%A4%A"),
                new UserInput("headers.X-Custom", "a+b%2Bc"),
                new UserInput("cookies.session", "abc%31%32%33"),
                new UserInput("body.cmd", "cmd"),
                new UserInput("body.cmd", "who%61mi"),
                new UserInput("body.literal", "literal"),
                new UserInput("body.literal", "a+b"),
                new UserInput("route.command", "whoami"),
                new UserInput("query.path", "../etc/passwd"),
                new UserInput("query.emoji", "\U0001F600"),
                new UserInput("query.double", "whoami"),
                new UserInput("headers.X-Custom", "a+b+c"),
                new UserInput("cookies.session", "abc123"),
                new UserInput("body.cmd", "whoami")
            }));
        }

        [Test]
        public async Task ReadAndFlattenHttpDataAsync_ShouldProcessReadableBodyWithoutContentLengthHint()
        {
            const string body = "{\"userCommand\":\"whoami\"}";
            using var bodyStream = new MemoryStream(Encoding.UTF8.GetBytes(body));

            var result = await HttpHelper.ReadAndFlattenHttpDataAsync(
                new Dictionary<string, string>(),
                new Dictionary<string, string>(),
                new Dictionary<string, string>(),
                new Dictionary<string, string>(),
                bodyStream,
                "application/json");

            Assert.That(result.FlattenedData.Where(input => input.Location == "body.userCommand").Select(input => input.Value),
                Is.EquivalentTo(new[] { "userCommand", "whoami" }));
            Assert.That(result.ParsedBody, Is.TypeOf<Dictionary<string, object>>());

            var parsedBody = (Dictionary<string, object>)result.ParsedBody;
            Assert.That(parsedBody["userCommand"]?.ToString(), Is.EqualTo("whoami"));
            Assert.That(bodyStream.Position, Is.EqualTo(0));
        }

        [TestCase("", false)]
        [TestCase(" \r\n\t", true)]
        [TestCase("{invalid", true)]
        [NonParallelizable]
        public async Task ReadAndFlattenHttpDataAsync_ShouldOnlyIgnoreEmptyJsonBodies(string body, bool shouldLogError)
        {
            var originalDebug = Environment.GetEnvironmentVariable("AIKIDO_DEBUG");
            var originalLogger = Agent.Logger;
            var logger = new Mock<ILogger>();

            try
            {
                Environment.SetEnvironmentVariable("AIKIDO_DEBUG", "true");
                Agent.ConfigureLogger(logger.Object);
                using var bodyStream = new MemoryStream(Encoding.UTF8.GetBytes(body));

                var result = await HttpHelper.ReadAndFlattenHttpDataAsync(
                    new Dictionary<string, string>(),
                    new Dictionary<string, string> { { "name", "updated" } },
                    new Dictionary<string, string>(),
                    new Dictionary<string, string>(),
                    bodyStream,
                    "application/json");

                Assert.That(result.ParsedBody, Is.Null);
                Assert.That(result.FlattenedData.Single(input => input.Location == "query.name").Value, Is.EqualTo("updated"));
                Assert.That(bodyStream.Position, Is.Zero);
                Assert.That(bodyStream.CanRead, Is.True);
                logger.Verify(l => l.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Exactly(shouldLogError ? 1 : 0));
            }
            finally
            {
                Agent.ConfigureLogger(originalLogger);
                Environment.SetEnvironmentVariable("AIKIDO_DEBUG", originalDebug);
            }
        }

        [TestCase(1024, true, false)]
        [TestCase(1025, true, false)]
        [TestCase(10000, true, false)]
        [TestCase(10001, true, false)]
        [TestCase(1024, false, false)]
        [TestCase(1025, false, false)]
        [TestCase(10000, false, false)]
        [TestCase(10001, false, false)]
        [TestCase(1024, true, true)]
        [TestCase(1025, true, true)]
        [TestCase(10001, true, true)]
        public async Task ReadAndFlattenHttpDataAsync_ShouldLimitFormValuesWithoutChangingBody(int count, bool repeatedKey, bool emptyValues)
        {
            var pairs = Enumerable.Range(0, count).Select(index =>
                $"{(repeatedKey ? "field" : $"field{index}")}={(emptyValues ? "" : "%2561")}");
            var body = string.Join("&", pairs);
            using var bodyStream = new MemoryStream(Encoding.UTF8.GetBytes(body));

            var result = await HttpHelper.ReadAndFlattenHttpDataAsync(
                new Dictionary<string, string>(),
                new Dictionary<string, string> { { "name", "updated" } },
                new Dictionary<string, string>(),
                new Dictionary<string, string>(),
                bodyStream,
                "application/x-www-form-urlencoded");

            Assert.That(result.FlattenedData.Single(input => input.Location == "query.name").Value, Is.EqualTo("updated"));
            if (count > 10000)
            {
                Assert.That(result.ParsedBody, Is.Null);
                Assert.That(result.FlattenedData.Any(input => input.Location.StartsWith("body.")), Is.False);
            }
            else
            {
                Assert.That(result.ParsedBody, Is.TypeOf<Dictionary<string, object>>());
                var bodyInputs = result.FlattenedData.Where(input => input.Location.StartsWith("body.")).ToList();
                Assert.That(bodyInputs.Count(input => input.Value == (emptyValues ? "" : "%61")), Is.EqualTo(count + (repeatedKey && emptyValues ? 1 : 0)));
                if (!emptyValues)
                {
                    Assert.That(bodyInputs.Count(input => input.Value == "a"), Is.EqualTo(count));
                }
            }

            Assert.That(bodyStream.Position, Is.Zero);
            Assert.That(bodyStream.CanRead, Is.True);
            using var reader = new StreamReader(bodyStream, leaveOpen: true);
            Assert.That(await reader.ReadToEndAsync(), Is.EqualTo(body));
        }

        [Test]
        public void ToJsonObj_ShouldHandleTrueFalseNull()
        {
            // Arrange
            var trueElement = JsonDocument.Parse("true").RootElement;
            var falseElement = JsonDocument.Parse("false").RootElement;
            var nullElement = JsonDocument.Parse("null").RootElement;

            // Act
            var trueResult = JsonHelper.ToJsonObj(trueElement);
            var falseResult = JsonHelper.ToJsonObj(falseElement);
            var nullResult = JsonHelper.ToJsonObj(nullElement);

            // Assert
            Assert.That(trueResult, Is.EqualTo(true));
            Assert.That(falseResult, Is.EqualTo(false));
            Assert.That(nullResult, Is.Null);
        }

        [TestCase("\"x' OR '1'='1\"", "body", "body", "x' OR '1'='1")]
        [TestCase("\"\\u002e\\u002e/appsettings.json\"", "body", "body", "../appsettings.json")]
        [TestCase("\"\"", "body", "body", "")]
        [TestCase("42", "body", "body", "42")]
        [TestCase("-1.25", "body", "body", "-1.25")]
        [TestCase("true", "body", "body", "True")]
        [TestCase("false", "body", "body", "False")]
        [TestCase("null", "body", "body", "")]
        [TestCase("\"value\"", null, "", "value")]
        [TestCase("\"value\"", "", "", "value")]
        [TestCase("\"value\"", "body", "body", "value")]
        public void FlattenJson_ShouldCaptureTopLevelScalars(string json, string? prefix, string expectedKey, string expectedValue)
        {
            using var document = JsonDocument.Parse(json);
            var result = new List<UserInput>();

            JsonHelper.FlattenJson(result, document.RootElement, prefix);

            Assert.That(result, Is.EquivalentTo(new List<UserInput> { new UserInput(expectedKey, expectedValue) }));
        }

        [Test]
        public void FlattenJson_ShouldHandleObjectsAndArrays()
        {
            // Arrange
            var json = "{\"key1\": {\"subkey\": \"value\"}, \"key2\": [\"item1\", \"item2\"]}";
            var element = JsonDocument.Parse(json).RootElement;
            var result = new List<UserInput>();

            // Act
            JsonHelper.FlattenJson(result, element, "");

            // Assert
            Assert.That(result, Is.EquivalentTo(new[]
            {
                new UserInput("key1", "key1"),
                new UserInput("key1.subkey", "subkey"),
                new UserInput("key1.subkey", "value"),
                new UserInput("key2", "key2"),
                new UserInput("key2.0", "item1"),
                new UserInput("key2.1", "item2")
            }));
        }

        [TestCase("plain text", "plain text")]
        [TestCase("<![CDATA[x' OR '1'='1]]>", "x' OR '1'='1")]
        [TestCase("<![CDATA[../appsettings.json]]>", "../appsettings.json")]
        [TestCase("x' <![CDATA[OR '1'='1]]>", "x' OR '1'='1")]
        [TestCase("<![CDATA[x' OR '1'='1]]> tail", "x' OR '1'='1 tail")]
        [TestCase("<![CDATA[..]]><![CDATA[/appsettings.json]]>", "../appsettings.json")]
        [TestCase("x'<![CDATA[ OR ]]>'1'='1", "x' OR '1'='1")]
        [TestCase("<![CDATA[]]>", "")]
        [TestCase("<![CDATA[  value  ]]>", "value")]
        [TestCase("a<!--ignored--><![CDATA[b]]>c", "abc")]
        public void FlattenXml_ShouldCaptureTextAndCData(string content, string expected)
        {
            var document = new XmlDocument();
            document.LoadXml($"<root>{content}</root>");
            var result = new List<UserInput>();

            XmlHelper.FlattenXml(result, document.DocumentElement!, "body");

            Assert.That(result, Is.EquivalentTo(new List<UserInput> { new UserInput("body", expected) }));
        }

        [Test]
        public void GetAttackSourceFromUserInputKey_ShouldIdentifyCorrectSource()
        {
            // Act & Assert
            Assert.That(UserInputHelper.GetAttackSourceFromUserInputKey("headers.someHeader"), Is.EqualTo(Source.Headers));
            Assert.That(UserInputHelper.GetAttackSourceFromUserInputKey("cookies.someCookie"), Is.EqualTo(Source.Cookies));
            Assert.That(UserInputHelper.GetAttackSourceFromUserInputKey("route.someRoute"), Is.EqualTo(Source.RouteParams));
        }

        [TestCase("multipart/form-data; boundary=b")]
        [TestCase("multipart/form-data; boundary=\"b\"")]
        [TestCase("multipart/form-data;boundary=\"b\"")]
        [TestCase("multipart/form-data; charset=utf-8; boundary=\"b\"")]
        public async Task ReadAndFlattenHttpDataAsync_ShouldReadMultipartWithQuotedBoundary(string contentType)
        {
            var payload = "--b\r\nContent-Disposition: form-data; name=\"x\"\r\n\r\n' OR 1=1 --\r\n--b--\r\n";
            using var bodyStream = new MemoryStream(Encoding.UTF8.GetBytes(payload));

            var result = await HttpHelper.ReadAndFlattenHttpDataAsync(
                new Dictionary<string, string>(),
                new Dictionary<string, string>(),
                new Dictionary<string, string>(),
                new Dictionary<string, string>(),
                bodyStream,
                contentType);

            Assert.That(result.FlattenedData.Any(input => input.Value == "' OR 1=1 --"), Is.True, contentType);
        }

        [Test]
        public void GetRawBody_ShouldReadStreamAndLeaveItOpen()
        {
            const string expected = "{\"name\":\"zen\"}";
            using var bodyStream = new MemoryStream(Encoding.UTF8.GetBytes(expected));

            var rawBody = HttpHelper.GetRawBody(bodyStream);

            Assert.That(rawBody, Is.EqualTo(expected));
            Assert.That(bodyStream.CanRead, Is.True);

            bodyStream.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(bodyStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            Assert.That(reader.ReadToEnd(), Is.EqualTo(expected));
        }
    }
}
