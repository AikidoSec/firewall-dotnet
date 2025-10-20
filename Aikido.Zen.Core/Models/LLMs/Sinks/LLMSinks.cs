using System.Collections.Generic;

namespace Aikido.Zen.Core.Models.LLMs.Sinks
{
    /// <summary>
    /// Holds all the LLM sinks of the providers we want to patch
    /// </summary>
    internal static class LLMSinks
    {
        public static IReadOnlyCollection<LLMSink> Sinks = new List<LLMSink>()
            {
                AddOpenAISink(),
                AddRystemOpenAISink(),
            };

        private static LLMSink AddOpenAISink()
        {
            return new LLMSink("OpenAI", new List<LLMMethod>
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

        private static LLMSink AddRystemOpenAISink()
        {
            return new LLMSink("Rystem.OpenAI", new List<LLMMethod>
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
