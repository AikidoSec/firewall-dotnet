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
                CreateOpenAISink(),
                CreateRystemOpenAISink(),
            };

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
