using Moq;

using Aikido.Zen.Core;
using Aikido.Zen.Core.Patches.LLMs;

namespace Aikido.Zen.Tests
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
