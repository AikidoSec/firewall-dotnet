using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Aikido.Zen.Core.Helpers;

[assembly: InternalsVisibleTo("Aikido.Zen.Test")]

namespace Aikido.Zen.Core.Patches
{
    public static class LLMSink
    {
        private const string OperationKind = "ai_op";

        [PatchTarget(PatchKind.Postfix, "OpenAI", "OpenAI.Chat.ChatClient", "CompleteChat")]
        [PatchTarget(PatchKind.Postfix, "OpenAI", "OpenAI.Chat.ChatClient", "CompleteChatAsync")]
        [PatchTarget(PatchKind.Postfix, "Rystem.OpenAi", "Rystem.OpenAi.Chat.OpenAiChat", "ExecuteAsync")]
        [PatchTarget(PatchKind.Postfix, "Rystem.OpenAi", "Rystem.OpenAi.Chat.OpenAiChat", "ExecuteAsStreamAsync")]
        private static void OnLLMCallCompleted(object[] __args, MethodBase __originalMethod, object __instance, object __result)
        {
            var assembly = __instance?.GetType().Assembly.FullName?.Split(new[] { ", Culture=" }, StringSplitOptions.RemoveEmptyEntries)[0] ?? string.Empty;
            var resolvedResult = LLMResultHelper.ResolveResult(__result);

            OnLLMCallCompleted(__args, __originalMethod, assembly, resolvedResult, Patcher.GetContext());
        }

        public static void OnLLMCallCompleted(object[] __args, MethodBase __originalMethod, string assembly, object result, Context context)
        {
            if (ReflectionHelper.ShouldSkipAssembly())
            {
                return;
            }

            try
            {
                var stopWatch = Stopwatch.StartNew();
                if (context == null || result == null)
                {
                    return;
                }

                if (!TryExtractModelFromResult(result, out var model))
                {
                    LogHelper.ErrorLog(Agent.Logger, $"Failed to extract model from LLM result for model: {model}");
                }

                if (!TryGetCloudProvider($"{model} {assembly} {result.GetType()}", out var provider))
                {
                    LogHelper.ErrorLog(Agent.Logger, $"Failed to extract provider from LLM for model: {model}, provider: {provider}");
                }

                if (!TryExtractTokensFromResult(result, out var tokens))
                {
                    LogHelper.ErrorLog(Agent.Logger, $"Failed to extract token usage from LLM result for provider: {provider}, model: {model}");
                }

                Agent.Instance.Context.OnAiCall(provider, model, tokens.inputTokens, tokens.outputTokens, context.Route);

                Agent.Instance.Context.OnInspectedCall(
                    operation: $"{__originalMethod.DeclaringType.Namespace}.{__originalMethod.DeclaringType.Name}.{__originalMethod.Name}",
                    kind: OperationKind,
                    durationInMs: stopWatch.ElapsedMilliseconds,
                    attackDetected: false,
                    blocked: false,
                    withoutContext: context != null
                );
            }
            catch
            {
                LogHelper.ErrorLog(Agent.Logger, "Error tracking LLM call statistics.");
            }
        }

        internal static bool TryGetCloudProvider(string searchString, out string provider)
        {
            provider = "unknown";
            searchString = searchString.ToLower();

            if (searchString.Contains("azure"))
            {
                provider = "azure";
                return true;
            }

            if (searchString.Contains("anthropic") || searchString.Contains("claude"))
            {
                provider = "anthropic";
                return true;
            }

            if (searchString.Contains("google") || searchString.Contains("gemini"))
            {
                provider = "gemini";
                return true;
            }

            if (searchString.Contains("mistral"))
            {
                provider = "mistral";
                return true;
            }

            if (searchString.Contains("openai"))
            {
                provider = "openai";
                return true;
            }

            return false;
        }

        internal static bool TryExtractModelFromResult(object result, out string model)
        {
            model = "unknown";
            try
            {
                var resultAsDictionary = ReflectionHelper.ConvertObjectToDictionary(result);
                if (resultAsDictionary.Count == 0)
                {
                    return false;
                }

                if (resultAsDictionary.TryGetValue("Model", out object modelAsObject) && modelAsObject != null)
                {
                    model = modelAsObject.ToString();
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        internal static bool TryExtractTokensFromResult(object result, out (long inputTokens, long outputTokens) tokens)
        {
            try
            {
                var resultAsDictionary = ReflectionHelper.ConvertObjectToDictionary(result);
                if (resultAsDictionary.Count == 0)
                {
                    tokens = (0, 0);
                    return false;
                }

                if (resultAsDictionary.TryGetValue("Usage", out object usage))
                {
                    var iTokensFound = false;
                    var oTokensFound = false;
                    var usageAsDictionary = ReflectionHelper.ConvertObjectToDictionary(usage);
                    long? inputTokens = null;
                    long? outputTokens = null;

                    if (usageAsDictionary.TryGetValue("InputTokenCount", out var input))
                    {
                        inputTokens = Convert.ToInt64(input);
                        iTokensFound = true;
                    }
                    else if (usageAsDictionary.TryGetValue("PromptTokens", out var prompt))
                    {
                        inputTokens = Convert.ToInt64(prompt);
                        iTokensFound = true;
                    }

                    if (usageAsDictionary.TryGetValue("OutputTokenCount", out var output))
                    {
                        outputTokens = Convert.ToInt64(output);
                        oTokensFound = true;
                    }
                    else if (usageAsDictionary.TryGetValue("CompletionTokens", out var completion))
                    {
                        outputTokens = Convert.ToInt64(completion);
                        oTokensFound = true;
                    }

                    tokens = (inputTokens ?? 0, outputTokens ?? 0);
                    return iTokensFound && oTokensFound;
                }
            }
            catch
            {
            }

            tokens = (0, 0);
            return false;
        }

    }
}
