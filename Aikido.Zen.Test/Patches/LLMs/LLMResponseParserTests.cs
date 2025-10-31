using Aikido.Zen.Core.Models.LLMs;
using Aikido.Zen.Core.Patches.LLMs.LLMResultParsers;

namespace Aikido.Zen.Tests;

internal class LLMResponseParserTests
{
    #region OPENAI PARSER TESTS
    [Test]
    public void Parse_OpenAIParser_ParsesExpectedResponse()
    {
        // Arrange
        var expected = new ParsedLLMResponseModel
        {
            Model = "gpt-4o-mini",
            TokenUsage = new TokenUsage
            {
                InputTokens = 150,
                OutputTokens = 75
            }
        };
        var openAIParser = new OpenAIResponseParser();
        var assemblyName = "OpenAI";
        var result = new
        {
            Value= new
            {
                Model = "gpt-4o-mini",
                Usage = new
                {
                    InputTokenCount = "150",
                    OutputTokenCount = "75"
                }
            }
        };
        // Act
        var parsed = openAIParser.Parse(result, assemblyName);

        // Assert: the fallback parser is used
        Assert.That(parsed.Model, Is.EqualTo(expected.Model));
        Assert.That(parsed.TokenUsage.InputTokens, Is.EqualTo(expected.TokenUsage.InputTokens));
        Assert.That(parsed.TokenUsage.OutputTokens, Is.EqualTo(expected.TokenUsage.OutputTokens));
    }

    [Test]
    public void Parse_OpenAIParser_ParsesUnexpectedModelResponse()
    {
        // Arrange
        var expected = new ParsedLLMResponseModel
        {
            Model = "unknown",
            TokenUsage = new TokenUsage
            {
                InputTokens = 150,
                OutputTokens = 75
            }
        };
        var openAIParser = new OpenAIResponseParser();
        var assemblyName = "OpenAI";
        var result = new
        {
            Value = new
            {
                WrongModel = "gpt-4o-mini",
                Usage = new
                {
                    InputTokenCount = "150",
                    OutputTokenCount = "75"
                }
            }
        };
        // Act
        var parsed = openAIParser.Parse(result, assemblyName);

        // Assert: the fallback parser is used
        Assert.That(parsed.Model, Is.EqualTo(expected.Model));
        Assert.That(parsed.TokenUsage.InputTokens, Is.EqualTo(expected.TokenUsage.InputTokens));
        Assert.That(parsed.TokenUsage.OutputTokens, Is.EqualTo(expected.TokenUsage.OutputTokens));
    }

    [Test]
    public void Parse_OpenAIParser_ParsesUnexpectedUsagelResponse()
    {
        // Arrange
        var expected = new ParsedLLMResponseModel
        {
            Model = "gpt-4o-mini",
            TokenUsage = new TokenUsage
            {
                InputTokens = 0,
                OutputTokens = 0
            }
        };
        var openAIParser = new OpenAIResponseParser();
        var assemblyName = "OpenAI";
        var result = new
        {
            Value = new
            {
                Model = "gpt-4o-mini",
                WrongUsage = new
                {
                    InputTokenCount = "150",
                    OutputTokenCount = "75"
                }
            }
        };
        // Act
        var parsed = openAIParser.Parse(result, assemblyName);

        // Assert: the fallback parser is used
        Assert.That(parsed.Model, Is.EqualTo(expected.Model));
        Assert.That(parsed.TokenUsage.InputTokens, Is.EqualTo(expected.TokenUsage.InputTokens));
        Assert.That(parsed.TokenUsage.OutputTokens, Is.EqualTo(expected.TokenUsage.OutputTokens));
    }

    [Test]
    public void Parse_OpenAIParser_ParsesUnexpectedInputTokenResponse()
    {
        // Arrange
        var expected = new ParsedLLMResponseModel
        {
            Model = "gpt-4o-mini",
            TokenUsage = new TokenUsage
            {
                InputTokens = 0,
                OutputTokens = 75
            }
        };
        var openAIParser = new OpenAIResponseParser();
        var assemblyName = "OpenAI";
        var result = new
        {
            Value = new
            {
                Model = "gpt-4o-mini",
                Usage = new
                {
                    WrongInputTokenCount = "150",
                    OutputTokenCount = "75"
                }
            }
        };
        // Act
        var parsed = openAIParser.Parse(result, assemblyName);

        // Assert: the fallback parser is used
        Assert.That(parsed.Model, Is.EqualTo(expected.Model));
        Assert.That(parsed.TokenUsage.InputTokens, Is.EqualTo(expected.TokenUsage.InputTokens));
        Assert.That(parsed.TokenUsage.OutputTokens, Is.EqualTo(expected.TokenUsage.OutputTokens));
    }

    [Test]
    public void Parse_OpenAIParser_ParsesUnexpectedOutputTokenResponse()
    {
        // Arrange
        var expected = new ParsedLLMResponseModel
        {
            Model = "gpt-4o-mini",
            TokenUsage = new TokenUsage
            {
                InputTokens = 150,
                OutputTokens = 0
            }
        };
        var openAIParser = new OpenAIResponseParser();
        var assemblyName = "OpenAI";        
        var result = new
        {
            Value = new
            {
                Model = "gpt-4o-mini",
                Usage = new
                {
                    InputTokenCount = "150",
                    WrongOutputTokenCount = "75"
                }
            }
        };
        // Act
        var parsed = openAIParser.Parse(result, assemblyName);

        // Assert: the fallback parser is used
        Assert.That(parsed.Model, Is.EqualTo(expected.Model));
        Assert.That(parsed.TokenUsage.InputTokens, Is.EqualTo(expected.TokenUsage.InputTokens));
        Assert.That(parsed.TokenUsage.OutputTokens, Is.EqualTo(expected.TokenUsage.OutputTokens));
    }
    #endregion

    #region RYSTEM PARSER TESTS
    [Test]
    public void Parse_RystemOpenAIParser_ParsesExpectedResponse()
    {
        // Arrange
        var expected = new ParsedLLMResponseModel
        {
            Model = "gpt-4o-mini",
            TokenUsage = new TokenUsage
            {
                InputTokens = 150,
                OutputTokens = 75
            }
        };
        var openAIParser = new RystemOpenAIResponseParser();
        var assemblyName = "Rystem.OpenAI";
        var result = new
        {
            Model = "gpt-4o-mini",
            Usage = new
            {
                PromptTokens = "150",
                CompletionTokens = "75"
            }
        };
        // Act
        var parsed = openAIParser.Parse(result, assemblyName);

        // Assert: the fallback parser is used
        Assert.That(parsed.Model, Is.EqualTo(expected.Model));
        Assert.That(parsed.TokenUsage.InputTokens, Is.EqualTo(expected.TokenUsage.InputTokens));
        Assert.That(parsed.TokenUsage.OutputTokens, Is.EqualTo(expected.TokenUsage.OutputTokens));
    }

    [Test]
    public void Parse_RystemOpenAIParser_ParsesUnexpectedModelResponse()
    {
        // Arrange
        var expected = new ParsedLLMResponseModel
        {
            Model = "unknown",
            TokenUsage = new TokenUsage
            {
                InputTokens = 150,
                OutputTokens = 75
            }
        };
        var openAIParser = new RystemOpenAIResponseParser();
        var assemblyName = "Rystem.OpenAI";
        var result = new
        {
            WrongModel = "gpt-4o-mini",
            Usage = new
            {
                PromptTokens = "150",
                CompletionTokens = "75"
            }
        };
        // Act
        var parsed = openAIParser.Parse(result, assemblyName);

        // Assert: the fallback parser is used
        Assert.That(parsed.Model, Is.EqualTo(expected.Model));
        Assert.That(parsed.TokenUsage.InputTokens, Is.EqualTo(expected.TokenUsage.InputTokens));
        Assert.That(parsed.TokenUsage.OutputTokens, Is.EqualTo(expected.TokenUsage.OutputTokens));
    }

    [Test]
    public void Parse_RystemOpenAIParser_ParsesUnexpectedUsagelResponse()
    {
        // Arrange
        var expected = new ParsedLLMResponseModel
        {
            Model = "gpt-4o-mini",
            TokenUsage = new TokenUsage
            {
                InputTokens = 0,
                OutputTokens = 0
            }
        };
        var openAIParser = new RystemOpenAIResponseParser();
        var assemblyName = "Rystem.OpenAI";
        var result = new
        {
            Model = "gpt-4o-mini",
            WrongUsage = new
            {
                PromptTokens = "150",
                CompletionTokens = "75"
            }
        };
        // Act
        var parsed = openAIParser.Parse(result, assemblyName);

        // Assert: the fallback parser is used
        Assert.That(parsed.Model, Is.EqualTo(expected.Model));
        Assert.That(parsed.TokenUsage.InputTokens, Is.EqualTo(expected.TokenUsage.InputTokens));
        Assert.That(parsed.TokenUsage.OutputTokens, Is.EqualTo(expected.TokenUsage.OutputTokens));
    }

    [Test]
    public void Parse_RystemAIParser_ParsesUnexpectedInputTokenResponse()
    {
        // Arrange
        var expected = new ParsedLLMResponseModel
        {
            Model = "gpt-4o-mini",
            TokenUsage = new TokenUsage
            {
                InputTokens = 0,
                OutputTokens = 75
            }
        };
        var openAIParser = new RystemOpenAIResponseParser();
        var assemblyName = "Rystem.OpenAI";
        var result = new
        {
            Model = "gpt-4o-mini",
            Usage = new
            {
                WrongPromptTokens = "150",
                CompletionTokens = "75"
            }
        };
        // Act
        var parsed = openAIParser.Parse(result, assemblyName);

        // Assert: the fallback parser is used
        Assert.That(parsed.Model, Is.EqualTo(expected.Model));
        Assert.That(parsed.TokenUsage.InputTokens, Is.EqualTo(expected.TokenUsage.InputTokens));
        Assert.That(parsed.TokenUsage.OutputTokens, Is.EqualTo(expected.TokenUsage.OutputTokens));
    }

    [Test]
    public void Parse_RystemOpenAIParser_ParsesUnexpectedOutputTokenResponse()
    {
        // Arrange
        var expected = new ParsedLLMResponseModel
        {
            Model = "gpt-4o-mini",
            TokenUsage = new TokenUsage
            {
                InputTokens = 150,
                OutputTokens = 0
            }
        };
        var openAIParser = new RystemOpenAIResponseParser();
        var assemblyName = "Rystem.OpenAI";
        var result = new
        {
            Model = "gpt-4o-mini",
            Usage = new
            {
               PromptTokens = "150",
               WrongCompletionTokens = "0"
            }
        };
        // Act
        var parsed = openAIParser.Parse(result, assemblyName);

        // Assert: the fallback parser is used
        Assert.That(parsed.Model, Is.EqualTo(expected.Model));
        Assert.That(parsed.TokenUsage.InputTokens, Is.EqualTo(expected.TokenUsage.InputTokens));
        Assert.That(parsed.TokenUsage.OutputTokens, Is.EqualTo(expected.TokenUsage.OutputTokens));
    }
    #endregion

    #region RYSTEM PARSER TESTS
    [Test]
    public void Parse_GenericParser_ParsesExpectedResponse()
    {
        // Arrange
        var expected = new ParsedLLMResponseModel
        {
            Model = "gpt-4o-mini",
            TokenUsage = new TokenUsage
            {
                InputTokens = 150,
                OutputTokens = 75
            }
        };
        var openAIParser = new GenericResponseParser();
        var assemblyName = "Generic.Assembly";
        var result = new
        {
            Model = "gpt-4o-mini",
            InputTokens = "150",
            OutputTokens = "75",
        };
        // Act
        var parsed = openAIParser.Parse(result, assemblyName);

        // Assert: the fallback parser is used
        Assert.That(parsed.Model, Is.EqualTo(expected.Model));
        Assert.That(parsed.TokenUsage.InputTokens, Is.EqualTo(expected.TokenUsage.InputTokens));
        Assert.That(parsed.TokenUsage.OutputTokens, Is.EqualTo(expected.TokenUsage.OutputTokens));
    }

    [Test]
    public void Parse_GenericParser_ParsesUnexpectedModelResponse()
    {
        // Arrange
        var expected = new ParsedLLMResponseModel
        {
            Model = "unknown",
            TokenUsage = new TokenUsage
            {
                InputTokens = 150,
                OutputTokens = 75
            }
        };
        var openAIParser = new GenericResponseParser();
        var assemblyName = "Generic.Assembly";
        var result = new
        {
            WrongModel = "gpt-4o-mini",
            InputTokens = "150",
            OutputTokens = "75",        
        };
        // Act
        var parsed = openAIParser.Parse(result, assemblyName);

        // Assert: the fallback parser is used
        Assert.That(parsed.Model, Is.EqualTo(expected.Model));
        Assert.That(parsed.TokenUsage.InputTokens, Is.EqualTo(expected.TokenUsage.InputTokens));
        Assert.That(parsed.TokenUsage.OutputTokens, Is.EqualTo(expected.TokenUsage.OutputTokens));
    }

    [Test]
    public void Parse_GenericParser_ParsesUnexpectedInputTokenResponse()
    {
        // Arrange
        var expected = new ParsedLLMResponseModel
        {
            Model = "gpt-4o-mini",
            TokenUsage = new TokenUsage
            {
                InputTokens = 0,
                OutputTokens = 75
            }
        };
        var openAIParser = new GenericResponseParser();
        var assemblyName = "Generic.Assembly";
        var result = new
        {
            Model = "gpt-4o-mini",
            WrongInputTokens = "150",
            OutputTokens = "75",
        };
        // Act
        var parsed = openAIParser.Parse(result, assemblyName);

        // Assert: the fallback parser is used
        Assert.That(parsed.Model, Is.EqualTo(expected.Model));
        Assert.That(parsed.TokenUsage.InputTokens, Is.EqualTo(expected.TokenUsage.InputTokens));
        Assert.That(parsed.TokenUsage.OutputTokens, Is.EqualTo(expected.TokenUsage.OutputTokens));
    }

    [Test]
    public void Parse_GenericParser_ParsesUnexpectedOutputTokenResponse()
    {
        // Arrange
        var expected = new ParsedLLMResponseModel
        {
            Model = "gpt-4o-mini",
            TokenUsage = new TokenUsage
            {
                InputTokens = 150,
                OutputTokens = 0
            }
        };
        var openAIParser = new GenericResponseParser();
        var assemblyName = "Generic.Assembly";
        var result = new
        {
            Model = "gpt-4o-mini",
            InputTokens = "150",
            WrongOutputTokens = "75",
        };
        // Act
        var parsed = openAIParser.Parse(result, assemblyName);

        // Assert: the fallback parser is used
        Assert.That(parsed.Model, Is.EqualTo(expected.Model));
        Assert.That(parsed.TokenUsage.InputTokens, Is.EqualTo(expected.TokenUsage.InputTokens));
        Assert.That(parsed.TokenUsage.OutputTokens, Is.EqualTo(expected.TokenUsage.OutputTokens));
    }
    #endregion
}
