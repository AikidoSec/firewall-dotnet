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
                new UserInput("body.cmd", "who%61mi"),
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

            Assert.That(result.FlattenedData.Single(input => input.Location == "body.userCommand").Value, Is.EqualTo("whoami"));
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

        [TestCase("\"x' OR '1'='1\"", "body", "body.value", "x' OR '1'='1")]
        [TestCase("\"\\u002e\\u002e/appsettings.json\"", "body", "body.value", "../appsettings.json")]
        [TestCase("\"\"", "body", "body.value", "")]
        [TestCase("42", "body", "body.value", "42")]
        [TestCase("-1.25", "body", "body.value", "-1.25")]
        [TestCase("true", "body", "body.value", "True")]
        [TestCase("false", "body", "body.value", "False")]
        [TestCase("null", "body", "body.value", "")]
        [TestCase("\"value\"", null, "value", "value")]
        [TestCase("\"value\"", "", "value", "value")]
        [TestCase("\"value\"", "body", "body.value", "value")]
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
            Assert.That(result.Single(input => input.Location == "key1.subkey").Value, Is.EqualTo("value"));
            Assert.That(result.Single(input => input.Location == "key2.0").Value, Is.EqualTo("item1"));
            Assert.That(result.Single(input => input.Location == "key2.1").Value, Is.EqualTo("item2"));
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

        // Zen must resolve a media type the way ASP.NET Core does, reading the leading type/subtype and
        // ignoring what trails it. Anything Zen fails to recognise but the framework still deserializes is
        // a body that goes uninspected.

        // Well-formed headers and the parameter section are unaffected.
        [TestCase("application/json", "application/json")]
        [TestCase("application/json; charset=utf-8", "application/json")]
        [TestCase("APPLICATION/JSON", "APPLICATION/JSON")]
        [TestCase("application/vnd.api+json", "application/vnd.api+json")]
        [TestCase("multipart/form-data; boundary=abc", "multipart/form-data")]
        // Trailing content after the media type.
        [TestCase("application/json _", "application/json")]
        [TestCase("application/json foo", "application/json")]
        [TestCase("application/json a b", "application/json")]
        [TestCase("application/json\t_", "application/json")]
        [TestCase("application/json,", "application/json")]
        [TestCase("application/json, application/json", "application/json")]
        [TestCase("application/vnd.api+json _", "application/vnd.api+json")]
        // Whitespace around the '/' - the framework tolerates it, so we do too.
        [TestCase("application /json", "application/json")]
        [TestCase("application /json _", "application/json")]
        [TestCase("application/ json _", "application/json")]
        [TestCase("application / json _", "application/json")]
        [TestCase("  application/json  ", "application/json")]
        // Characters RFC 7230 excludes from a token end the media type just as whitespace does.
        [TestCase("application/json\"x", "application/json")]
        [TestCase("application/json(x)", "application/json")]
        [TestCase("application/json<x", "application/json")]
        [TestCase("application/json>x", "application/json")]
        [TestCase("application/json@x", "application/json")]
        [TestCase("application/json[x]", "application/json")]
        [TestCase("application/json\\x", "application/json")]
        [TestCase("application/json?x", "application/json")]
        [TestCase("application/json=x", "application/json")]
        [TestCase("application/json:x", "application/json")]
        [TestCase("application/json{x", "application/json")]
        // Not a media type at all - must not be coerced into one.
        [TestCase("", "")]
        [TestCase("   ", "")]
        [TestCase("nonsense", "")]
        [TestCase("application/", "")]
        [TestCase("/json", "")]
        [TestCase("/", "")]
        [TestCase("; charset=utf-8", "")]
        // Distinct media types must stay distinct.
        [TestCase("application/jsonp", "application/jsonp")]
        [TestCase("application/notjson", "application/notjson")]
        [TestCase("application/json-patch", "application/json-patch")]
        [TestCase("text/html", "text/html")]
        public void GetMediaType_ReturnsLeadingTypeSubtype(string contentType, string expected)
        {
            Assert.That(HttpHelper.GetMediaType(contentType), Is.EqualTo(expected));
        }

        [Test]
        public void GetMediaType_HandlesNull()
        {
            Assert.That(HttpHelper.GetMediaType(null), Is.EqualTo(string.Empty));
        }

    }
}
