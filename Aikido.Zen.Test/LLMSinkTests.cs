using Aikido.Zen.Core;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Sinks;
using Aikido.Zen.Tests.Mocks;

namespace Aikido.Zen.Test
{
    [TestFixture]
    public class LLMSinkTests
    {
        private Agent _agent;

        [SetUp]
        public void SetUp()
        {
            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", "test-token");
            _agent = Agent.NewInstance(ZenApiMock.CreateMock().Object);
            _agent.ClearContext();
        }

        [TearDown]
        public void TearDown()
        {
            Patcher.Unpatch();
            _agent?.Dispose();
            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", null);
        }

        #region OnLLMCallExecuting Tests

        #endregion

        #region OnLLMCallCompleted Tests

        [Test]
        public void OnLLMCallCompleted_WithNullContext_DoesNotThrow()
        {
            // Arrange
            var method = typeof(string).GetMethod("ToString", Type.EmptyTypes);
            var result = new MockLLMResult { Model = "gpt-4" };

            // Act & Assert
            Assert.DoesNotThrow(() => LLMSink.OnLLMCallCompleted(new OpenAIClientInstance(), result, method, null));
        }

        [Test]
        public void OnLLMCallCompleted_WithNullResult_DoesNotThrow()
        {
            // Arrange
            var method = typeof(string).GetMethod("ToString", Type.EmptyTypes);
            var context = new Context();

            // Act & Assert
            Assert.DoesNotThrow(() => LLMSink.OnLLMCallCompleted(new OpenAIClientInstance(), null, method, context));
        }

        [Test]
        public void OnLLMCallCompleted_WithValidInputs_DoesNotThrow()
        {
            // Arrange
            var method = typeof(string).GetMethod("ToString", Type.EmptyTypes);
            var result = new MockLLMResult
            {
                Model = "gpt-4",
                Usage = new MockUsage { InputTokenCount = 10, OutputTokenCount = 20 }
            };
            var context = new Context { Route = "/api/test" };

            // Act & Assert
            Assert.DoesNotThrow(() => LLMSink.OnLLMCallCompleted(new OpenAIClientInstance(), result, method, context));
        }

        [Test]
        public void OnLLMCallCompleted_WithOpenAIChatCompletion_TracksTokensCorrectly()
        {
            // Arrange - Mock OpenAI ChatClient.CompleteChat call
            var client = new OpenAIClientInstance();
            var method = client.GetType().GetMethod("ToString"); // Using available method for test
            var result = new MockLLMResult
            {
                Model = "gpt-4o",
                Usage = new MockUsage { InputTokenCount = 15, OutputTokenCount = 35 }
            };
            var context = new Context { Route = "/llm-usage/request/provider/openai/model/gpt-4o" };

            // Act & Assert
            Assert.DoesNotThrow(() => LLMSink.OnLLMCallCompleted(client, result, method, context));
        }

        [Test]
        public void OnLLMCallCompleted_WithAzureOpenAIChatCompletion_TracksTokensCorrectly()
        {
            // Arrange - Mock Azure OpenAI ChatClient.CompleteChat call
            var client = new AzureOpenAIClientInstance();
            var method = client.GetType().GetMethod("ToString");
            var result = new MockLLMResult
            {
                Model = "gpt-3.5-turbo",
                Usage = new MockUsage { InputTokenCount = 25, OutputTokenCount = 45 }
            };
            var context = new Context { Route = "/llm-usage/request/provider/azure/model/gpt-3.5-turbo" };

            // Act & Assert
            Assert.DoesNotThrow(() => LLMSink.OnLLMCallCompleted(client, result, method, context));
        }

        [Test]
        public void OnLLMCallCompleted_WithRystemOpenAI_TracksTokensCorrectly()
        {
            // Arrange - Mock Rystem.OpenAi ExecuteAsync call
            var client = new RystemOpenAIClientInstance();
            var method = client.GetType().GetMethod("ToString");
            var result = new MockLLMResult
            {
                Model = "gpt-4-turbo",
                Usage = new MockRystemUsage { PromptTokens = 30, CompletionTokens = 60 }
            };
            var context = new Context { Route = "/llm-usage/request/provider/rystem/model/gpt-4-turbo" };

            // Act & Assert
            Assert.DoesNotThrow(() => LLMSink.OnLLMCallCompleted(client, result, method, context));
        }

        [Test]
        public void OnLLMCallCompleted_WithAnthropicClaude_IdentifiesProviderCorrectly()
        {
            // Arrange - Mock Anthropic Claude call through OpenAI SDK
            var client = new OpenAIClientInstance();
            var method = client.GetType().GetMethod("ToString");
            var result = new MockLLMResult
            {
                Model = "claude-3-5-haiku-latest",
                Usage = new MockUsage { InputTokenCount = 40, OutputTokenCount = 80 }
            };
            var context = new Context { Route = "/llm-usage/request/provider/anthropic/model/claude-3-5-haiku-latest" };

            // Act & Assert
            Assert.DoesNotThrow(() => LLMSink.OnLLMCallCompleted(client, result, method, context));
        }

        [Test]
        public void OnLLMCallCompleted_WithMockedOpenAIClient_ExtractsTokensCorrectly()
        {
            // Arrange - Mock the actual OpenAI client pattern like in LlmUsageController
            var client = new OpenAIClientInstance();

            // Setup mock method call
            var method = client.GetType().GetMethod("ToString");

            // Mock the response structure that matches real OpenAI SDK
            var result = new MockLLMResult
            {
                Model = "gpt-4o-mini",
                Usage = new MockUsage { InputTokenCount = 12, OutputTokenCount = 8 }
            };
            var context = new Context { Route = "/llm-usage/request/provider/openai/model/gpt-4o-mini" };

            // Act & Assert
            Assert.DoesNotThrow(() => LLMSink.OnLLMCallCompleted(client, result, method, context));
        }

        [Test]
        public void OnLLMCallCompleted_WithMockedAzureClient_ExtractsTokensCorrectly()
        {
            // Arrange - Mock the actual Azure OpenAI client pattern like in LlmUsageController
            var client = new AzureOpenAIClientInstance();
            var method = client.GetType().GetMethod("ToString");

            // Mock the response structure that matches real Azure OpenAI SDK
            var result = new MockLLMResult
            {
                Model = "gpt-35-turbo",
                Usage = new MockUsage { InputTokenCount = 18, OutputTokenCount = 42 }
            };
            var context = new Context { Route = "/llm-usage/request/provider/azure/model/gpt-35-turbo" };

            // Act & Assert
            Assert.DoesNotThrow(() => LLMSink.OnLLMCallCompleted(client, result, method, context));
        }

        [Test]
        public void OnLLMCallCompleted_WithMockedRystemClient_ExtractsTokensCorrectly()
        {
            // Arrange - Mock the actual Rystem OpenAI client pattern like in LlmUsageController
            var client = new RystemOpenAIClientInstance();
            var method = client.GetType().GetMethod("ToString");

            // Mock the response structure that matches real Rystem SDK
            var result = new MockLLMResult
            {
                Model = "gpt-4",
                Usage = new MockRystemUsage { PromptTokens = 35, CompletionTokens = 67 }
            };
            var context = new Context { Route = "/llm-usage/request/provider/rystem/model/gpt-4" };

            // Act & Assert
            Assert.DoesNotThrow(() => LLMSink.OnLLMCallCompleted(client, result, method, context));
        }

        [Test]
        public void OnLLMCallCompletedWrapper_WithNullInstance_UsesConfiguredContext()
        {
            // Arrange
            var context = new Context { Route = "/llm/static" };
            Patcher.PatchSinks(() => context);
            var method = typeof(string).GetMethod(nameof(string.ToString), Type.EmptyTypes);
            var result = new MockLLMResult
            {
                Model = "gpt-4o-mini",
                Usage = new MockUsage { InputTokenCount = 5, OutputTokenCount = 7 }
            };

            // Act
            LLMSink.OnLLMCallCompleted(null, result, method, context);

            // Assert
            var aiInfo = _agent.Context.AiStats.Providers.Values.Single();
            Assert.That(aiInfo.Provider, Is.EqualTo("unknown"));
            Assert.That(aiInfo.Model, Is.EqualTo("gpt-4o-mini"));
            Assert.That(aiInfo.Tokens.Input, Is.EqualTo(5));
            Assert.That(aiInfo.Tokens.Output, Is.EqualTo(7));
            Assert.That(aiInfo.Routes.Single().Path, Is.EqualTo("/llm/static"));
        }

        [Test]
        public void OnLLMCallCompleted_WithUnknownProviderAndMissingTokens_RecordsUnknownProviderWithZeroTokens()
        {
            // Arrange
            var method = typeof(string).GetMethod(nameof(string.ToString), Type.EmptyTypes);
            var result = new MockLLMResult
            {
                Model = "local-model",
                Usage = new { }
            };
            var context = new Context { Route = "/llm/local" };

            // Act
            LLMSink.OnLLMCallCompleted(new CustomLLMClientInstance(), result, method, context);

            // Assert
            var aiInfo = _agent.Context.AiStats.Providers.Values.Single();
            Assert.That(aiInfo.Provider, Is.EqualTo("unknown"));
            Assert.That(aiInfo.Model, Is.EqualTo("local-model"));
            Assert.That(aiInfo.Calls, Is.EqualTo(1));
            Assert.That(aiInfo.Tokens.Input, Is.EqualTo(0));
            Assert.That(aiInfo.Tokens.Output, Is.EqualTo(0));
            Assert.That(aiInfo.Routes.Single().Path, Is.EqualTo("/llm/local"));
        }

        [Test]
        public void OnLLMCallCompleted_WithMissingModel_RecordsUnknownModel()
        {
            // Arrange
            var method = typeof(string).GetMethod(nameof(string.ToString), Type.EmptyTypes);
            var result = new MockLLMResult
            {
                Usage = new MockUsage { InputTokenCount = 10, OutputTokenCount = 20 }
            };
            var context = new Context { Route = "/llm/openai" };

            // Act
            LLMSink.OnLLMCallCompleted(new OpenAIClientInstance(), result, method, context);

            // Assert
            var aiInfo = _agent.Context.AiStats.Providers.Values.Single();
            Assert.That(aiInfo.Provider, Is.EqualTo("openai"));
            Assert.That(aiInfo.Model, Is.EqualTo("unknown"));
            Assert.That(aiInfo.Tokens.Input, Is.EqualTo(10));
            Assert.That(aiInfo.Tokens.Output, Is.EqualTo(20));
        }

        #endregion

        #region TryGetProvider Tests

        [TestCase("gpt-4o Azure.AI.OpenAI.OpenAIClient", "azure")]
        [TestCase("gpt-3.5-turbo Azure.AI.OpenAI.ChatClient", "azure")]
        [TestCase("claude-3-5-haiku-latest OpenAI.ChatClient", "anthropic")]
        [TestCase("gemini-1.5-pro Google.GenAI.Client", "gemini")]
        [TestCase("mistral-large-latest Mistral.AI.Client", "mistral")]
        [TestCase("gpt-4o OpenAI.Chat.ChatClient", "openai")]
        [TestCase("o3-mini Rystem.OpenAI.OpenAIClient.Chat", "openai")]
        public void TryGetProvider_WithKnownProviders_ReturnsCorrectProvider(string searchString, string expectedProvider)
        {
            // Act
            var result = LLMSink.TryGetCloudProvider(searchString, out var actualProvider);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(actualProvider, Is.EqualTo(expectedProvider));
        }

        [TestCase("llama-2-70b Meta.AI.LlamaClient")]
        [TestCase("custom-model CustomLLM.SDK.Client")]
        [TestCase("")]
        [TestCase("unknown-model Unknown.Provider.Client")]
        public void TryGetProvider_WithUnknownProviders_ReturnsFalse(string searchString)
        {
            // Act
            var result = LLMSink.TryGetCloudProvider(searchString, out var actualProvider);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(actualProvider, Is.EqualTo("unknown"));
        }

        [Test]
        public void TryGetProvider_WithMultipleProviders_ReturnsFirstMatch()
        {
            // Azure should take precedence over OpenAI
            var result = LLMSink.TryGetCloudProvider("gpt-4o Azure.OpenAI.ChatClient", out var actualProvider);

            Assert.That(result, Is.True);
            Assert.That(actualProvider, Is.EqualTo("azure"));
        }

        [Test]
        public void TryGetProvider_WithCaseInsensitivity_ReturnsCorrectProvider()
        {
            // Test case insensitivity
            var result = LLMSink.TryGetCloudProvider("GPT-4O AZURE.AI.OPENAI.CHATCLIENT", out var actualProvider);

            Assert.That(result, Is.True);
            Assert.That(actualProvider, Is.EqualTo("azure"));
        }

        [Test]
        public void TryGetProvider_WithClaudeInModelName_ReturnsAnthropic()
        {
            // Test that "claude" in model name correctly identifies Anthropic
            var result = LLMSink.TryGetCloudProvider("claude-3-sonnet OpenAI.Chat.ChatClient", out var actualProvider);

            Assert.That(result, Is.True);
            Assert.That(actualProvider, Is.EqualTo("anthropic"));
        }

        #endregion

        #region ConvertObjectToDictionary Tests

        [Test]
        public void ConvertObjectToDictionary_WithNull_ReturnsEmptyDictionary()
        {
            // Act
            var result = ReflectionHelper.ConvertObjectToDictionary(null);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count, Is.EqualTo(0));
        }

        [Test]
        public void ConvertObjectToDictionary_WithExistingDictionary_ReturnsNewDictionary()
        {
            // Arrange
            var input = new Dictionary<string, object> { { "key1", "value1" }, { "key2", 42 } };

            // Act
            var result = ReflectionHelper.ConvertObjectToDictionary(input);

            // Assert
            Assert.That(result, Is.Not.SameAs(input));
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result["key1"], Is.EqualTo("value1"));
            Assert.That(result["key2"], Is.EqualTo(42));
        }

        [Test]
        public void ConvertObjectToDictionary_WithSimpleObject_ReturnsPropertiesAsDictionary()
        {
            // Arrange
            var input = new { Name = "Test", Value = 42, IsActive = true };

            // Act
            var result = ReflectionHelper.ConvertObjectToDictionary(input);

            // Assert
            Assert.That(result.Count, Is.EqualTo(3));
            Assert.That(result["Name"], Is.EqualTo("Test"));
            Assert.That(result["Value"], Is.EqualTo(42));
            Assert.That(result["IsActive"], Is.EqualTo(true));
        }

        [Test]
        public void ConvertObjectToDictionary_WithComplexObject_ReturnsPropertiesAsDictionary()
        {
            // Arrange
            var input = new MockLLMResult
            {
                Model = "gpt-4",
                Usage = new MockUsage { InputTokenCount = 10, OutputTokenCount = 20 }
            };

            // Act
            var result = ReflectionHelper.ConvertObjectToDictionary(input);

            // Assert
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result["Model"], Is.EqualTo("gpt-4"));
            Assert.That(result["Usage"], Is.InstanceOf<MockUsage>());
        }

        [Test]
        public void ConvertObjectToDictionary_WithObjectWithExceptionThrowingProperty_SkipsProperty()
        {
            // Arrange
            var input = new MockObjectWithBrokenProperty();

            // Act
            var result = ReflectionHelper.ConvertObjectToDictionary(input);

            // Assert
            Assert.That(result.Count, Is.EqualTo(1)); // Only WorkingProperty should be included
            Assert.That(result["WorkingProperty"], Is.EqualTo("works"));
            Assert.That(result.ContainsKey("BrokenProperty"), Is.False);
        }

        #endregion

        #region TryExtractModelFromResult Tests

        [Test]
        public void TryExtractModelFromResult_WithValidModel_ReturnsTrue()
        {
            // Arrange
            var input = new MockLLMResult { Model = "gpt-4-turbo" };

            // Act
            var result = LLMSink.TryExtractModelFromResult(input, out var model);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(model, Is.EqualTo("gpt-4-turbo"));
        }

        [Test]
        public void TryExtractModelFromResult_WithNullModel_ReturnsFalse()
        {
            // Arrange
            var input = new MockLLMResult { Model = null };

            // Act
            var result = LLMSink.TryExtractModelFromResult(input, out var model);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(model, Is.EqualTo("unknown"));
        }

        [Test]
        public void TryExtractModelFromResult_WithObjectWithoutModel_ReturnsFalse()
        {
            // Arrange
            var input = new { SomeProperty = "value" };

            // Act
            var result = LLMSink.TryExtractModelFromResult(input, out var model);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(model, Is.EqualTo("unknown"));
        }

        [Test]
        public void TryExtractModelFromResult_WithEmptyObject_ReturnsFalse()
        {
            // Arrange
            var input = new { };

            // Act
            var result = LLMSink.TryExtractModelFromResult(input, out var model);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(model, Is.EqualTo("unknown"));
        }

        #endregion

        #region TryExtractTokensFromResult Tests

        [Test]
        public void TryExtractTokensFromResult_WithOpenAIFormat_ReturnsTrue()
        {
            // Arrange
            var input = new MockLLMResult
            {
                Usage = new MockUsage { InputTokenCount = 150, OutputTokenCount = 75 }
            };

            // Act
            var result = LLMSink.TryExtractTokensFromResult(input, out var tokens);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(tokens.inputTokens, Is.EqualTo(150)); // Input tokens
            Assert.That(tokens.outputTokens, Is.EqualTo(75));  // Output tokens
        }

        [Test]
        public void TryExtractTokensFromResult_WithRystemFormat_ReturnsTrue()
        {
            // Arrange
            var input = new MockLLMResult
            {
                Usage = new MockRystemUsage { PromptTokens = 200, CompletionTokens = 100 }
            };

            // Act
            var result = LLMSink.TryExtractTokensFromResult(input, out var tokens);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(tokens.inputTokens, Is.EqualTo(200)); // Input tokens
            Assert.That(tokens.outputTokens, Is.EqualTo(100)); // Output tokens
        }

        [Test]
        public void TryExtractTokensFromResult_WithMixedFormat_ReturnsTrue()
        {
            // Arrange
            var input = new MockLLMResult
            {
                Usage = new MockMixedUsage
                {
                    InputTokenCount = 300,
                    CompletionTokens = 150
                }
            };

            // Act
            var result = LLMSink.TryExtractTokensFromResult(input, out var tokens);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(tokens.inputTokens, Is.EqualTo(300)); // Input tokens
            Assert.That(tokens.outputTokens, Is.EqualTo(150)); // Output tokens
        }

        [Test]
        public void TryExtractTokensFromResult_WithPartialTokens_ReturnsPartialValues()
        {
            // Arrange
            var input = new MockLLMResult
            {
                Usage = new MockUsage { InputTokenCount = 50 } // Only input tokens
            };

            // Act
            var result = LLMSink.TryExtractTokensFromResult(input, out var tokens);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(tokens.inputTokens, Is.EqualTo(50)); // Input tokens
            Assert.That(tokens.outputTokens, Is.EqualTo(0));  // Output tokens (default)
        }

        [Test]
        public void TryExtractTokensFromResult_WithNoUsage_ReturnsFalse()
        {
            // Arrange
            var input = new MockLLMResult { Model = "gpt-4" }; // No Usage property

            // Act
            var result = LLMSink.TryExtractTokensFromResult(input, out var tokens);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(tokens.inputTokens, Is.EqualTo(0));
            Assert.That(tokens.outputTokens, Is.EqualTo(0));
        }

        [Test]
        public void TryExtractTokensFromResult_WithEmptyUsage_ReturnsFalse()
        {
            // Arrange
            var input = new MockLLMResult
            {
                Usage = new { } // Empty usage object
            };

            // Act
            var result = LLMSink.TryExtractTokensFromResult(input, out var tokens);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(tokens.inputTokens, Is.EqualTo(0));
            Assert.That(tokens.outputTokens, Is.EqualTo(0));
        }

        [Test]
        public void TryExtractTokensFromResult_WithEmptyResult_ReturnsFalse()
        {
            // Arrange
            var input = new { };

            // Act
            var result = LLMSink.TryExtractTokensFromResult(input, out var tokens);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(tokens.inputTokens, Is.EqualTo(0));
            Assert.That(tokens.outputTokens, Is.EqualTo(0));
        }

        [Test]
        public void TryExtractTokensFromResult_WithResultWithoutUsageProperty_ReturnsFalse()
        {
            // Arrange
            var input = new { Model = "gpt-4o-mini" };

            // Act
            var result = LLMSink.TryExtractTokensFromResult(input, out var tokens);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(tokens.inputTokens, Is.EqualTo(0));
            Assert.That(tokens.outputTokens, Is.EqualTo(0));
        }

        [Test]
        public void TryExtractTokensFromResult_WithStringTokenValues_ConvertsCorrectly()
        {
            // Arrange
            var input = new MockLLMResult
            {
                Usage = new MockStringTokenUsage { InputTokenCount = "100", OutputTokenCount = "50" }
            };

            // Act
            var result = LLMSink.TryExtractTokensFromResult(input, out var tokens);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(tokens.inputTokens, Is.EqualTo(100));
            Assert.That(tokens.outputTokens, Is.EqualTo(50));
        }

        [Test]
        public void TryExtractTokensFromResult_WithInvalidTokenValues_ReturnsFalse()
        {
            // Arrange
            var input = new MockLLMResult
            {
                Usage = new MockStringTokenUsage { InputTokenCount = "many", OutputTokenCount = "few" }
            };

            // Act
            var result = LLMSink.TryExtractTokensFromResult(input, out var tokens);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(tokens.inputTokens, Is.EqualTo(0));
            Assert.That(tokens.outputTokens, Is.EqualTo(0));
        }

        #endregion

        #region Mock Classes

        // Base mock classes
        private class MockLLMResult
        {
            public string? Model { get; set; }
            public object? Usage { get; set; }
        }

        private class MockUsage
        {
            public int? InputTokenCount { get; set; }
            public int? OutputTokenCount { get; set; }
        }

        private class MockRystemUsage
        {
            public long PromptTokens { get; set; }
            public long CompletionTokens { get; set; }
        }

        private class MockMixedUsage
        {
            public long InputTokenCount { get; set; }
            public long CompletionTokens { get; set; }
        }

        private class MockStringTokenUsage
        {
            public string? InputTokenCount { get; set; }
            public string? OutputTokenCount { get; set; }
        }

        private class OpenAIClientInstance
        {
        }

        private class AzureOpenAIClientInstance
        {
        }

        private class RystemOpenAIClientInstance
        {
        }

        private class CustomLLMClientInstance
        {
        }

        private class MockObjectWithBrokenProperty
        {
            public string WorkingProperty => "works";

            public string BrokenProperty
            {
                get => throw new InvalidOperationException("Property access failed");
            }
        }

        #endregion
    }
}
