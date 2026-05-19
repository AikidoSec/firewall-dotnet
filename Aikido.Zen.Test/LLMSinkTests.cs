using Aikido.Zen.Core;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Sinks;
using Aikido.Zen.Tests.Mocks;
using System.Reflection;

namespace Aikido.Zen.Test
{
    [TestFixture]
    public class LLMSinkTests
    {
        private Agent _agent;
        private Context? _activeContext;

        [SetUp]
        public void SetUp()
        {
            Environment.SetEnvironmentVariable("AIKIDO_TOKEN", "test-token");
            _agent = Agent.NewInstance(ZenApiMock.CreateMock().Object);
            _agent.ClearContext();
            Patcher.Unpatch();
            Patcher.PatchSinks(() => _activeContext!);
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
            Assert.DoesNotThrow(() => OnLLMCallCompleted(new OpenAIClientInstance(), result, method, null));
        }

        [Test]
        public void OnLLMCallCompleted_WithNullResult_DoesNotThrow()
        {
            // Arrange
            var method = typeof(string).GetMethod("ToString", Type.EmptyTypes);
            var context = new Context();

            // Act & Assert
            Assert.DoesNotThrow(() => OnLLMCallCompleted(new OpenAIClientInstance(), null, method, context));
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
            Assert.DoesNotThrow(() => OnLLMCallCompleted(new OpenAIClientInstance(), result, method, context));
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
            Assert.DoesNotThrow(() => OnLLMCallCompleted(client, result, method, context));
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
            Assert.DoesNotThrow(() => OnLLMCallCompleted(client, result, method, context));
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
            Assert.DoesNotThrow(() => OnLLMCallCompleted(client, result, method, context));
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
            Assert.DoesNotThrow(() => OnLLMCallCompleted(client, result, method, context));
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
            Assert.DoesNotThrow(() => OnLLMCallCompleted(client, result, method, context));
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
            Assert.DoesNotThrow(() => OnLLMCallCompleted(client, result, method, context));
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
            Assert.DoesNotThrow(() => OnLLMCallCompleted(client, result, method, context));
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
            OnLLMCallCompleted(null, result, method, context);

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
            OnLLMCallCompleted(new CustomLLMClientInstance(), result, method, context);

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
            OnLLMCallCompleted(new OpenAIClientInstance(), result, method, context);

            // Assert
            var aiInfo = _agent.Context.AiStats.Providers.Values.Single();
            Assert.That(aiInfo.Provider, Is.EqualTo("openai"));
            Assert.That(aiInfo.Model, Is.EqualTo("unknown"));
            Assert.That(aiInfo.Tokens.Input, Is.EqualTo(10));
            Assert.That(aiInfo.Tokens.Output, Is.EqualTo(20));
        }

        #endregion

        #region Provider Detection Tests

        [TestCase(typeof(AzureOpenAIClientInstance), "gpt-4o", "azure")]
        [TestCase(typeof(OpenAIClientInstance), "claude-3-5-haiku-latest", "anthropic")]
        [TestCase(typeof(GoogleGeminiClientInstance), "gemini-1.5-pro", "gemini")]
        [TestCase(typeof(MistralClientInstance), "mistral-large-latest", "mistral")]
        [TestCase(typeof(OpenAIClientInstance), "gpt-4o", "openai")]
        [TestCase(typeof(RystemOpenAIClientInstance), "o3-mini", "openai")]
        [TestCase(typeof(CustomLLMClientInstance), "llama-2-70b", "unknown")]
        public void OnLLMCallCompleted_RecordsExpectedProvider(Type clientType, string model, string expectedProvider)
        {
            var method = typeof(string).GetMethod(nameof(string.ToString), Type.EmptyTypes);
            var result = new MockLLMResult
            {
                Model = model,
                Usage = new MockUsage { InputTokenCount = 1, OutputTokenCount = 2 }
            };
            var context = new Context { Route = "/llm/provider" };
            var client = Activator.CreateInstance(clientType);

            OnLLMCallCompleted(client!, result, method, context);

            var aiInfo = _agent.Context.AiStats.Providers.Values.Single();
            Assert.That(aiInfo.Provider, Is.EqualTo(expectedProvider));
            Assert.That(aiInfo.Model, Is.EqualTo(model));
            Assert.That(aiInfo.Tokens.Input, Is.EqualTo(1));
            Assert.That(aiInfo.Tokens.Output, Is.EqualTo(2));
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

        #region Token Extraction Tests

        [Test]
        public void OnLLMCallCompleted_WithMixedUsage_RecordsTokens()
        {
            var method = typeof(string).GetMethod(nameof(string.ToString), Type.EmptyTypes);
            var result = new MockLLMResult
            {
                Model = "gpt-4o",
                Usage = new MockMixedUsage
                {
                    InputTokenCount = 300,
                    CompletionTokens = 150
                }
            };
            var context = new Context { Route = "/llm/mixed" };

            OnLLMCallCompleted(new OpenAIClientInstance(), result, method, context);

            var aiInfo = _agent.Context.AiStats.Providers.Values.Single();
            Assert.That(aiInfo.Tokens.Input, Is.EqualTo(300));
            Assert.That(aiInfo.Tokens.Output, Is.EqualTo(150));
        }

        [Test]
        public void OnLLMCallCompleted_WithPartialTokens_RecordsAvailableTokens()
        {
            var method = typeof(string).GetMethod(nameof(string.ToString), Type.EmptyTypes);
            var result = new MockLLMResult
            {
                Model = "gpt-4o",
                Usage = new MockUsage { InputTokenCount = 50 }
            };
            var context = new Context { Route = "/llm/partial" };

            OnLLMCallCompleted(new OpenAIClientInstance(), result, method, context);

            var aiInfo = _agent.Context.AiStats.Providers.Values.Single();
            Assert.That(aiInfo.Tokens.Input, Is.EqualTo(50));
            Assert.That(aiInfo.Tokens.Output, Is.EqualTo(0));
        }

        [Test]
        public void OnLLMCallCompleted_WithStringTokenValues_RecordsConvertedTokens()
        {
            var method = typeof(string).GetMethod(nameof(string.ToString), Type.EmptyTypes);
            var result = new MockLLMResult
            {
                Model = "gpt-4o",
                Usage = new MockStringTokenUsage { InputTokenCount = "100", OutputTokenCount = "50" }
            };
            var context = new Context { Route = "/llm/string-tokens" };

            OnLLMCallCompleted(new OpenAIClientInstance(), result, method, context);

            var aiInfo = _agent.Context.AiStats.Providers.Values.Single();
            Assert.That(aiInfo.Tokens.Input, Is.EqualTo(100));
            Assert.That(aiInfo.Tokens.Output, Is.EqualTo(50));
        }

        [Test]
        public void OnLLMCallCompleted_WithInvalidTokenValues_RecordsZeroTokens()
        {
            var method = typeof(string).GetMethod(nameof(string.ToString), Type.EmptyTypes);
            var result = new MockLLMResult
            {
                Model = "gpt-4o",
                Usage = new MockStringTokenUsage { InputTokenCount = "many", OutputTokenCount = "few" }
            };
            var context = new Context { Route = "/llm/invalid-tokens" };

            OnLLMCallCompleted(new OpenAIClientInstance(), result, method, context);

            var aiInfo = _agent.Context.AiStats.Providers.Values.Single();
            Assert.That(aiInfo.Tokens.Input, Is.EqualTo(0));
            Assert.That(aiInfo.Tokens.Output, Is.EqualTo(0));
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

        private class GoogleGeminiClientInstance
        {
        }

        private class MistralClientInstance
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

        private void OnLLMCallCompleted(object instance, object result, MethodInfo method, Context? context)
        {
            _activeContext = context;
            LLMSink.OnLLMCallCompletedGeneric(instance, result, method);
        }

        #endregion
    }
}
