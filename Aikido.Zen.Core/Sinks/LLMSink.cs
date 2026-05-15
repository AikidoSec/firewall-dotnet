using System;
using System.Reflection;
using System.Runtime.CompilerServices;

using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;

[assembly: InternalsVisibleTo("Aikido.Zen.Test")]

namespace Aikido.Zen.Core.Sinks
{
    /// <summary>
    /// Patches for LLM client operations to track and monitor LLM API calls
    /// </summary>
    internal static class LLMSink
    {
        private const string OperationKind = "ai_op";

        [SinkPostfix("OpenAI", "OpenAI.Chat.ChatClient", "CompleteChat")]
        [SinkPostfix("OpenAI", "OpenAI.Chat.ChatClient", "CompleteChatAsync")]
        [SinkPostfix("Rystem.OpenAi", "Rystem.OpenAi.Chat.OpenAiChat", "ExecuteAsync")]
        [SinkPostfix("Rystem.OpenAi", "Rystem.OpenAi.Chat.OpenAiChat", "ExecuteAsStreamAsync")]
        internal static void OnLLMCallCompletedGeneric(object __instance, object __result, MethodBase __originalMethod)
        {
            SinkAnalyzer.Analyze(
                __originalMethod,
                OperationKind,
                context => OnLLMCallCompleted(__instance, __result, context));
        }

        /// <summary>
        /// Handles completed LLM API calls to extract token usage and track statistics
        /// </summary>
        /// <param name="instance">The LLM client instance.</param>
        /// <param name="result">The result returned by the LLM API call.</param>
        /// <param name="context">The current Aikido context.</param>
        private static InspectionResult OnLLMCallCompleted(object instance, object result, Context context)
        {
            result = LLMResultHelper.ResolveResult(result);
            if (context == null || result == null)
            {
                return InspectionResult.Skip();
            }

            var clientType = instance?.GetType();

            if (!TryExtractModelFromResult(result, out var model))
            {
                LogHelper.ErrorLog(Agent.Logger, $"Failed to extract model from LLM result for model: {model}");
            }

            if (!TryGetCloudProvider($"{model} {clientType}", out var provider))
            {
                LogHelper.ErrorLog(Agent.Logger, $"Failed to extract provider from LLM for model: {model}, provider: {provider}");
            }

            if (!TryExtractTokensFromResult(result, out var tokens))
            {
                LogHelper.ErrorLog(Agent.Logger, $"Failed to extract token usage from LLM result for provider: {provider}, model: {model}");
            }

            // Record AI statistics
            Agent.Instance.Context.OnAiCall(provider, model, tokens.inputTokens, tokens.outputTokens, context.Route);

            return InspectionResult.Continue();
        }

        /// <summary>
        /// Extracts the cloud provider name
        /// </summary>
        /// <param name="searchString">The search string to extract the provider from.</param>
        /// <param name="provider">The extracted provider name.</param>
        /// <returns>True if the provider was extracted successfully, false otherwise. Not being used at the moment.</returns>
        private static bool TryGetCloudProvider(string searchString, out string provider)
        {
            provider = "unknown";
            searchString = searchString.ToLower();
            // first the cloud providers
            if (searchString.Contains("azure"))
            {
                provider = "azure";
                return true;
            }
            // then the llm companies
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
            // openai last, since their sdk get's used a lot for other llm providers
            if (searchString.Contains("openai"))
            {
                provider = "openai";
                return true;
            }
            return false;
        }

        /// <summary>
        /// Extracts the model name from the result based on the provider
        /// </summary>
        private static bool TryExtractModelFromResult(object result, out string model)
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

        private static bool TryExtractTokensFromResult(object result, out (long inputTokens, long outputTokens) tokens)
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

                    // OpenAI / Azure OpenAI client
                    if (usageAsDictionary.TryGetValue("InputTokenCount", out var input))
                    {
                        inputTokens = Convert.ToInt64(input);
                        iTokensFound = true;
                    }
                    // Rystem.OpenAi client
                    else if (usageAsDictionary.TryGetValue("PromptTokens", out var prompt))
                    {
                        inputTokens = Convert.ToInt64(prompt);
                        iTokensFound = true;
                    }

                    // OpenAI / Azure OpenAI client
                    if (usageAsDictionary.TryGetValue("OutputTokenCount", out var output))
                    {
                        outputTokens = Convert.ToInt64(output);
                        oTokensFound = true;
                    }
                    // Rystem.OpenAi client
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
                // pass through
            }
            tokens = (0, 0); // Default values if extraction fails
            return false;
        }
    }
}
