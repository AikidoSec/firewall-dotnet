using System.Reflection;

namespace Aikido.Zen.Core.Sinks
{
    internal static class LLMPatches
    {
        [SinkPostfix("OpenAI", "OpenAI.Chat.ChatClient", "CompleteChat")]
        [SinkPostfix("OpenAI", "OpenAI.Chat.ChatClient", "CompleteChatAsync")]
        [SinkPostfix("Rystem.OpenAi", "Rystem.OpenAi.Chat.OpenAiChat", "ExecuteAsync")]
        [SinkPostfix("Rystem.OpenAi", "Rystem.OpenAi.Chat.OpenAiChat", "ExecuteAsStreamAsync")]
        internal static void LLMCallCompleted(object __instance, object __result, MethodBase __originalMethod)
        {
            LLMSink.OnLLMCallCompleted(__instance, __result, __originalMethod, Patcher.GetContext());
        }
    }
}
