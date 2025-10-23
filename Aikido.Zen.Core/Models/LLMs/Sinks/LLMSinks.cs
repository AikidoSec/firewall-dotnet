using System.Collections.Generic;

namespace Aikido.Zen.Core.Models.LLMs.Sinks
{
    /// <summary>
    /// // Collection of provider LLM sinks that we enumerate to apply runtime patches and instrumentation.
    /// </summary>
    internal static class LLMSinks
    {
        public static IReadOnlyCollection<LLMSink> Sinks = new List<LLMSink>()
            {
                CreateAnthropicSink(),
                CreateAwsBedrockSink(),
                CreateAzureOpenAISink(),
                CreateOpenAISink(),
                CreateRystemOpenAISink(),
            };
        private static LLMSink CreateAnthropicSink()
        {
            return new LLMSink("AWSSDK.BedrockRuntime", LLMProviderEnum.Anthropic, new List<LLMMethod>
            {
                // Converse (normalized chat API)
                new LLMMethod(
                    "Converse",
                    "Amazon.BedrockRuntime.AmazonBedrockRuntimeClient",
                    new[]
                    {
                        "Amazon.BedrockRuntime.Model.ConverseRequest"
                    }
                ),
                new LLMMethod(
                    "ConverseAsync",
                    "Amazon.BedrockRuntime.AmazonBedrockRuntimeClient",
                    new[]
                    {
                        "Amazon.BedrockRuntime.Model.ConverseRequest",
                        "System.Threading.CancellationToken"
                    }
                ),

                // Converse streaming
                new LLMMethod(
                    "ConverseStream",
                    "Amazon.BedrockRuntime.AmazonBedrockRuntimeClient",
                    new[]
                    {
                        "Amazon.BedrockRuntime.Model.ConverseStreamRequest"
                    }
                ),
                new LLMMethod(
                    "ConverseStreamAsync",
                    "Amazon.BedrockRuntime.AmazonBedrockRuntimeClient",
                    new[]
                    {
                        "Amazon.BedrockRuntime.Model.ConverseStreamRequest",
                        "System.Threading.CancellationToken"
                    }
                ),

                // Generic model invocation
                new LLMMethod(
                    "InvokeModel",
                    "Amazon.BedrockRuntime.AmazonBedrockRuntimeClient",
                    new[]
                    {
                        "Amazon.BedrockRuntime.Model.InvokeModelRequest"
                    }
                ),
                new LLMMethod(
                    "InvokeModelAsync",
                    "Amazon.BedrockRuntime.AmazonBedrockRuntimeClient",
                    new[]
                    {
                        "Amazon.BedrockRuntime.Model.InvokeModelRequest",
                        "System.Threading.CancellationToken"
                    }
                ),

                // Streaming invocation
                new LLMMethod(
                    "InvokeModelWithResponseStream",
                    "Amazon.BedrockRuntime.AmazonBedrockRuntimeClient",
                    new[]
                    {
                        "Amazon.BedrockRuntime.Model.InvokeModelWithResponseStreamRequest"
                    }
                ),
                new LLMMethod(
                    "InvokeModelWithResponseStreamAsync",
                    "Amazon.BedrockRuntime.AmazonBedrockRuntimeClient",
                    new[]
                    {
                        "Amazon.BedrockRuntime.Model.InvokeModelWithResponseStreamRequest",
                        "System.Threading.CancellationToken"
                    }
                ),
            });
        }
        
        private static LLMSink CreateAwsBedrockSink()
        {
            return new LLMSink("Rystem.OpenAI", LLMProviderEnum.RystemOpenAI, new List<LLMMethod>
                {
                    new LLMMethod(
                        "ExecuteAsync",
                        "Rystem.OpenAi.Chat.OpenAiChat",
                        new[]
                        {
                            "System.Threading.CancellationToken"
                        }
                    ),
                    new LLMMethod(
                        "ExecuteAsStreamAsync",
                        "Rystem.OpenAi.Chat.OpenAiChat",
                        new[]
                        {
                            "System.Threading.CancellationToken"
                        }
                    )
                });
        }
        private static LLMSink CreateAzureOpenAISink()
        {
            return new LLMSink("Rystem.OpenAI", LLMProviderEnum.RystemOpenAI, new List<LLMMethod>
                {
                    new LLMMethod(
                        "ExecuteAsync",
                        "Rystem.OpenAi.Chat.OpenAiChat",
                        new[]
                        {
                            "System.Threading.CancellationToken"
                        }
                    ),
                    new LLMMethod(
                        "ExecuteAsStreamAsync",
                        "Rystem.OpenAi.Chat.OpenAiChat",
                        new[]
                        {
                            "System.Threading.CancellationToken"
                        }
                    )
                });
        }
        private static LLMSink CreateOpenAISink()
        {
            return new LLMSink("OpenAI", LLMProviderEnum.OpenAI, new List<LLMMethod>
                {
                    new LLMMethod(
                        "CompleteChat",
                        "OpenAI.Chat.ChatClient",
                        new[]
                        {
                            "System.Collections.Generic.IEnumerable`1[OpenAI.Chat.ChatMessage]",
                            "OpenAI.Chat.ChatCompletionOptions",
                            "System.Threading.CancellationToken"
                        }
                    ),
                    new LLMMethod(
                        "CompleteChatAsync",
                        "OpenAI.Chat.ChatClient",
                        new[]
                        {
                            "System.Collections.Generic.IEnumerable`1[OpenAI.Chat.ChatMessage]",
                            "OpenAI.Chat.ChatCompletionOptions",
                            "System.Threading.CancellationToken"
                        }
                    )
                });
        }
        private static LLMSink CreateRystemOpenAISink()
        {
            return new LLMSink("Rystem.OpenAI", LLMProviderEnum.RystemOpenAI, new List<LLMMethod>
                {
                    new LLMMethod(
                        "ExecuteAsync",
                        "Rystem.OpenAi.Chat.OpenAiChat",
                        new[]
                        {
                            "System.Threading.CancellationToken"
                        }
                    ),
                    new LLMMethod(
                        "ExecuteAsStreamAsync",
                        "Rystem.OpenAi.Chat.OpenAiChat",
                        new[]
                        {
                            "System.Threading.CancellationToken"
                        }
                    )
                });
        }



        
    }
}
