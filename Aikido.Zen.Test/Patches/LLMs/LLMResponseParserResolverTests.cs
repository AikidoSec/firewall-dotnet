using System.Reflection;

using Aikido.Zen.Core.Models.LLMs;
using Aikido.Zen.Core.Patches.LLMs;
using Aikido.Zen.Core.Patches.LLMs.LLMResultParsers.Abstractions;

namespace Aikido.Zen.Tests;

[TestFixture]
internal class LLMResponseParserResolverTests
{
    private List<ILLMResponseParser> _registryParsersSnapshot;
    private List<ILLMResponseParser> _registryParsers;

    [SetUp]
    public void SetUp()
    {
        // Grab the private static list from LLMResponseParserResolver
        var field = typeof(LLMResponseParserResolver).GetField("_parsers", BindingFlags.NonPublic | BindingFlags.Static);
        _registryParsers = (List<ILLMResponseParser>)field.GetValue(null);
        _registryParsersSnapshot = new List<ILLMResponseParser>(_registryParsers);
    }

    [TearDown]
    public void TearDown()
    {
        _registryParsers.Clear();
        _registryParsers.AddRange(_registryParsersSnapshot);
    }

    [Test]
    public void Parse_UnwrapsTaskResultValueBeforeParsing()
    {
        // Arrange: parser that only matches after Task unwrap (inner Value is the sentinel string)
        var expected = new ParsedLLMResponseModel();
        var parser = new TestParser(
            canParse: (result, assembly) => assembly == "TestProvider" && result is string s && s == "inner-value",
            parse: (result, assembly) => expected
        );
        _registryParsers.Insert(0, parser);

        var assembly = "TestProvider";
        var wrapped = new ResponseShim<string> { Value = "inner-value" };
        var task = Task.FromResult(wrapped); // Task with Result, whose Value is the actual LLM response

        // Act
        var parsed = LLMResponseParserResolver.Parse(task, assembly);

        // Assert
        Assert.That(expected, Is.SameAs(parsed));
    }

    [Test]
    public void Parse_ReturnsGenericParserResult_WhenNoParsersMatch()
    {
        // Arrange
        _registryParsers.Clear();

        var nonMatching = new TestParser(
            canParse: (result, assembly) => false,
            parse: (result, assembly) => throw new AssertionException("Non-matching parser should not parse")
        );

        var expected = new ParsedLLMResponseModel();
        var genericFallback = new TestParser(
            canParse: (result, assembly) => true, // emulate GenericResponseParser.CanParse == true
            parse: (result, assembly) => expected
        );

        _registryParsers.Add(nonMatching);
        _registryParsers.Add(genericFallback);

        var assemblyName = "Test.Provider";
        var result = new object();

        // Act
        var parsed = LLMResponseParserResolver.Parse(result, assemblyName);

        // Assert: the fallback parser is used
        Assert.That(parsed, Is.SameAs(expected));
    }

    [Test]
    public void Parse_ReturnsCorrectParserResult()
    {
        // Arrange
        _registryParsers.Clear();

        var nonMatching = new TestParser(
            canParse: (result, assembly) => false,
            parse: (result, assembly) => throw new AssertionException("Non-matching parser should not parse")
        );

        var assemblyName = "Test";
        var expected = new ParsedLLMResponseModel();
        var matching = new TestParser(
            canParse: (result, assembly) => assembly == assemblyName,
            parse: (result, assembly) => expected
        );

        var genericResponse = new ParsedLLMResponseModel();
        var genericFallback = new TestParser(
            canParse: (result, assembly) => true,
            parse: (result, assembly) => genericResponse
        );

        _registryParsers.Add(nonMatching);
        _registryParsers.Add(matching);
        _registryParsers.Add(genericFallback);

        var result = new object();

        // Act
        var parsed = LLMResponseParserResolver.Parse(result, assemblyName);

        // Assert: the fallback parser is used
        Assert.That(parsed, Is.SameAs(expected));
    }


    private sealed class TestParser : ILLMResponseParser
    {
        private readonly Func<object, string, bool> _canParse;
        private readonly Func<object, string, ParsedLLMResponseModel> _parse;

        public TestParser(Func<object, string, bool> canParse, Func<object, string, ParsedLLMResponseModel> parse)
        {
            _canParse = canParse;
            _parse = parse;
        }

        public bool CanParse(object result, string assembly) => _canParse(result, assembly);

        public ParsedLLMResponseModel Parse(object result, string assembly) => _parse(result, assembly);
    }

    private sealed class ResponseShim<T>
    {
        public T Value { get; set; }
    }
}
