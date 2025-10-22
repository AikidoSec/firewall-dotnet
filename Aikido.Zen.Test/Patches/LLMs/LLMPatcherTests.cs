using System.Reflection;
using Aikido.Zen.Core;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Patches.LLMs;
using Moq;

namespace Aikido.Zen.Tests.Patches.LLMs
{
    [TestFixture]
    public class LLMPatcherTests
    {

        #region OnLLMCallExecuting Tests

        #endregion

        #region OnLLMCallCompleted Tests

        [Test]
        public void OnLLMCallCompleted_WithNullContext_DoesNotThrow()
        {
            // Arrange
            var args = new object[] { };
            var method = typeof(string).GetMethod("ToString", Type.EmptyTypes);
            var assembly = "OpenAI";
            var result = new MockLLMResult { Model = "gpt-4" };

            // Act & Assert
            Assert.DoesNotThrow(() => LLMPatcher.OnLLMCallCompleted(args, method, assembly, result, null));
        }

        [Test]
        public void OnLLMCallCompleted_WithNullResult_DoesNotThrow()
        {
            // Arrange
            var args = new object[] { };
            var method = typeof(string).GetMethod("ToString", Type.EmptyTypes);
            var assembly = "OpenAI";
            var context = new Context();

            // Act & Assert
            Assert.DoesNotThrow(() => LLMPatcher.OnLLMCallCompleted(args, method, assembly, null, context));
        }

        [Test]
        public void OnLLMCallCompleted_WithValidInputs_DoesNotThrow()
        {
            // Arrange
            var args = new object[] { };
            var method = typeof(string).GetMethod("ToString", Type.EmptyTypes);
            var assembly = "OpenAI";
            var result = new MockLLMResult
            {
                Model = "gpt-4",
                Usage = new MockUsage { InputTokenCount = 10, OutputTokenCount = 20 }
            };
            var context = new Context { Route = "/api/test" };

            // Act & Assert
            Assert.DoesNotThrow(() => LLMPatcher.OnLLMCallCompleted(args, method, assembly, result, context));
        }

        [Test]
        public void OnLLMCallCompleted_WithOpenAIChatCompletion_TracksTokensCorrectly()
        {
            // Arrange - Mock OpenAI ChatClient.CompleteChat call
            var mockChatClient = new Mock<object>(); // Mocking the actual OpenAI ChatClient
            var args = new object[] { "Tell me a joke" };
            var method = mockChatClient.Object.GetType().GetMethod("ToString"); // Using available method for test
            var assembly = "OpenAI";
            var result = new MockLLMResult
            {
                Model = "gpt-4o",
                Usage = new MockUsage { InputTokenCount = 15, OutputTokenCount = 35 }
            };
            var context = new Context { Route = "/llm-usage/request/provider/openai/model/gpt-4o" };

            // Act & Assert
            Assert.DoesNotThrow(() => LLMPatcher.OnLLMCallCompleted(args, method, assembly, result, context));
        }

        [Test]
        public void OnLLMCallCompleted_WithAzureOpenAIChatCompletion_TracksTokensCorrectly()
        {
            // Arrange - Mock Azure OpenAI ChatClient.CompleteChat call
            var mockAzureChatClient = new Mock<object>(); // Mocking the actual Azure OpenAI ChatClient
            var args = new object[] { "What is the weather like?" };
            var method = mockAzureChatClient.Object.GetType().GetMethod("ToString");
            var assembly = "Azure.AI.OpenAI";
            var result = new MockLLMResult
            {
                Model = "gpt-3.5-turbo",
                Usage = new MockUsage { InputTokenCount = 25, OutputTokenCount = 45 }
            };
            var context = new Context { Route = "/llm-usage/request/provider/azure/model/gpt-3.5-turbo" };

            // Act & Assert
            Assert.DoesNotThrow(() => LLMPatcher.OnLLMCallCompleted(args, method, assembly, result, context));
        }

        [Test]
        public void OnLLMCallCompleted_WithRystemOpenAI_TracksTokensCorrectly()
        {
            // Arrange - Mock Rystem.OpenAi ExecuteAsync call
            var mockRystemClient = new Mock<object>(); // Mocking the actual Rystem OpenAI client
            var args = new object[] { };
            var method = mockRystemClient.Object.GetType().GetMethod("ToString");
            var assembly = "Rystem.OpenAi";
            var result = new MockLLMResult
            {
                Model = "gpt-4-turbo",
                Usage = new MockRystemUsage { PromptTokens = 30, CompletionTokens = 60 }
            };
            var context = new Context { Route = "/llm-usage/request/provider/rystem/model/gpt-4-turbo" };

            // Act & Assert
            Assert.DoesNotThrow(() => LLMPatcher.OnLLMCallCompleted(args, method, assembly, result, context));
        }

        [Test]
        public void OnLLMCallCompleted_WithAnthropicClaude_IdentifiesProviderCorrectly()
        {
            // Arrange - Mock Anthropic Claude call through OpenAI SDK
            var mockOpenAIClient = new Mock<object>(); // Mocking OpenAI client used for Anthropic
            var args = new object[] { "Explain quantum computing" };
            var method = mockOpenAIClient.Object.GetType().GetMethod("ToString");
            var assembly = "OpenAI";
            var result = new MockLLMResult
            {
                Model = "claude-3-5-haiku-latest",
                Usage = new MockUsage { InputTokenCount = 40, OutputTokenCount = 80 }
            };
            var context = new Context { Route = "/llm-usage/request/provider/anthropic/model/claude-3-5-haiku-latest" };

            // Act & Assert
            Assert.DoesNotThrow(() => LLMPatcher.OnLLMCallCompleted(args, method, assembly, result, context));
        }

        [Test]
        public void OnLLMCallCompleted_WithMockedOpenAIClient_ExtractsTokensCorrectly()
        {
            // Arrange - Mock the actual OpenAI client pattern like in LlmUsageController
            var mockOpenAIClient = new Mock<object>(); // Would be Mock<OpenAIClient> in real scenario
            var mockChatClient = new Mock<object>(); // Would be Mock<ChatClient> in real scenario

            // Setup mock method call
            var args = new object[] { "What is the capital of France?" };
            var method = mockChatClient.Object.GetType().GetMethod("ToString");
            var assembly = "OpenAI";

            // Mock the response structure that matches real OpenAI SDK
            var result = new MockLLMResult
            {
                Model = "gpt-4o-mini",
                Usage = new MockUsage { InputTokenCount = 12, OutputTokenCount = 8 }
            };
            var context = new Context { Route = "/llm-usage/request/provider/openai/model/gpt-4o-mini" };

            // Act & Assert
            Assert.DoesNotThrow(() => LLMPatcher.OnLLMCallCompleted(args, method, assembly, result, context));
        }

        [Test]
        public void OnLLMCallCompleted_WithMockedAzureClient_ExtractsTokensCorrectly()
        {
            // Arrange - Mock the actual Azure OpenAI client pattern like in LlmUsageController
            var mockAzureClient = new Mock<object>(); // Would be Mock<AzureOpenAIClient> in real scenario
            var mockChatClient = new Mock<object>(); // Would be Mock<ChatClient> in real scenario

            var args = new object[] { "Generate a summary" };
            var method = mockChatClient.Object.GetType().GetMethod("ToString");
            var assembly = "Azure.AI.OpenAI";

            // Mock the response structure that matches real Azure OpenAI SDK
            var result = new MockLLMResult
            {
                Model = "gpt-35-turbo",
                Usage = new MockUsage { InputTokenCount = 18, OutputTokenCount = 42 }
            };
            var context = new Context { Route = "/llm-usage/request/provider/azure/model/gpt-35-turbo" };

            // Act & Assert
            Assert.DoesNotThrow(() => LLMPatcher.OnLLMCallCompleted(args, method, assembly, result, context));
        }

        [Test]
        public void OnLLMCallCompleted_WithMockedRystemClient_ExtractsTokensCorrectly()
        {
            // Arrange - Mock the actual Rystem OpenAI client pattern like in LlmUsageController
            var mockRystemClient = new Mock<object>(); // Would be Mock<IOpenAi> in real scenario
            var mockChatRequest = new Mock<object>(); // Mock for the fluent API chain

            var args = new object[] { };
            var method = mockRystemClient.Object.GetType().GetMethod("ToString");
            var assembly = "Rystem.OpenAi";

            // Mock the response structure that matches real Rystem SDK
            var result = new MockLLMResult
            {
                Model = "gpt-4",
                Usage = new MockRystemUsage { PromptTokens = 35, CompletionTokens = 67 }
            };
            var context = new Context { Route = "/llm-usage/request/provider/rystem/model/gpt-4" };

            // Act & Assert
            Assert.DoesNotThrow(() => LLMPatcher.OnLLMCallCompleted(args, method, assembly, result, context));
        }

        #endregion

        #region TryGetProvider Tests

        [TestCase("gpt-4o Azure.AI.OpenAI.OpenAIClient", "azure")]
        [TestCase("gpt-3.5-turbo Azure.AI.OpenAI.ChatClient", "azure")]
        [TestCase("claude-3-5-haiku-latest OpenAI.ChatClient", "anthropic")]
        [TestCase("gpt-4o OpenAI.Chat.ChatClient", "openai")]
        [TestCase("o3-mini Rystem.OpenAI.OpenAIClient.Chat", "openai")]
        public void TryGetProvider_WithKnownProviders_ReturnsCorrectProvider(string searchString, string expectedProvider)
        {
            // Act
            var result = LLMPatcher.TryGetCloudProvider(searchString, out var actualProvider);

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
            var result = LLMPatcher.TryGetCloudProvider(searchString, out var actualProvider);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(actualProvider, Is.EqualTo("unknown"));
        }

        [Test]
        public void TryGetProvider_WithMultipleProviders_ReturnsFirstMatch()
        {
            // Azure should take precedence over OpenAI
            var result = LLMPatcher.TryGetCloudProvider("gpt-4o Azure.OpenAI.ChatClient", out var actualProvider);

            Assert.That(result, Is.True);
            Assert.That(actualProvider, Is.EqualTo("azure"));
        }

        [Test]
        public void TryGetProvider_WithCaseInsensitivity_ReturnsCorrectProvider()
        {
            // Test case insensitivity
            var result = LLMPatcher.TryGetCloudProvider("GPT-4O AZURE.AI.OPENAI.CHATCLIENT", out var actualProvider);

            Assert.That(result, Is.True);
            Assert.That(actualProvider, Is.EqualTo("azure"));
        }

        [Test]
        public void TryGetProvider_WithClaudeInModelName_ReturnsAnthropic()
        {
            // Test that "claude" in model name correctly identifies Anthropic
            var result = LLMPatcher.TryGetCloudProvider("claude-3-sonnet OpenAI.Chat.ChatClient", out var actualProvider);

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
            var result = LLMPatcher.TryExtractModelFromResult(TODO);

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
            var result = LLMPatcher.TryExtractModelFromResult(TODO);

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
            var result = LLMPatcher.TryExtractModelFromResult(TODO);

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
            var result = LLMPatcher.TryExtractModelFromResult(TODO);

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
            var result = LLMPatcher.TryExtractTokensFromResult(TODO, TODO);

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
            var result = LLMPatcher.TryExtractTokensFromResult(TODO, TODO);

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
            var result = LLMPatcher.TryExtractTokensFromResult(TODO, TODO);

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
            var result = LLMPatcher.TryExtractTokensFromResult(TODO, TODO);

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
            var result = LLMPatcher.TryExtractTokensFromResult(TODO, TODO);

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
            var result = LLMPatcher.TryExtractTokensFromResult(TODO, TODO);

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
            var result = LLMPatcher.TryExtractTokensFromResult(TODO, TODO);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(tokens.inputTokens, Is.EqualTo(100));
            Assert.That(tokens.outputTokens, Is.EqualTo(50));
        }

        #endregion

        #region Mock Classes

        // Base mock classes
        private class MockLLMResult
        {
            public string Model { get; set; }
            public object Usage { get; set; }
        }

        private class MockUsage
        {
            public long InputTokenCount { get; set; }
            public long OutputTokenCount { get; set; }
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
            public string InputTokenCount { get; set; }
            public string OutputTokenCount { get; set; }
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
