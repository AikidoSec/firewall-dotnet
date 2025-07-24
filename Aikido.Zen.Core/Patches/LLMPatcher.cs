using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;

[assembly: InternalsVisibleTo("Aikido.Zen.Test")]

namespace Aikido.Zen.Core.Patches
{
    /// <summary>
    /// Patches for LLM client operations to track and monitor LLM API calls
    /// </summary>
    public static class LLMPatcher
    {
        private const string operationKind = "ai_op";

        /// <summary>
        /// Handles completed LLM API calls to extract token usage and track statistics
        /// </summary>
        /// <param name="__args">The arguments passed to the method.</param>
        /// <param name="__originalMethod">The original method being patched.</param>
        /// <param name="messages">The chat messages sent to the LLM.</param>
        /// <param name="assembly">The assembly name containing the LLM client.</param>
        /// <param name="result">The result returned by the LLM API call.</param>
        /// <param name="context">The current Aikido context.</param>
        public static void OnLLMCallCompleted(object[] __args, MethodBase __originalMethod, string assembly, object result, Context context)
        {
            try
            {
                var stopWatch = Stopwatch.StartNew();
                if (context == null || result == null) return;


                if (!TryExtractModelFromResult(result, out var model))
                {
                    LogHelper.ErrorLog(Agent.Logger, $"Failed to extract model from LLM result for model: {model}");
                }

                if (!TryExtractTokensFromResult(result, out var tokens))
                {
                    LogHelper.ErrorLog(Agent.Logger, $"Failed to extract token usage from LLM result for provider: {assembly}, model: {model}");
                }

                // Record AI statistics
                Agent.Instance.Context.OnAiCall(assembly, model, tokens.inputTokens, tokens.outputTokens, context.Route);


                // record sink statistics
                Agent.Instance.Context.OnInspectedCall(
                    operation: $"{__originalMethod.DeclaringType.Namespace}.{__originalMethod.DeclaringType.Name}.{__originalMethod.Name}",
                    kind: operationKind,
                    durationInMs: stopWatch.ElapsedMilliseconds,
                    attackDetected: false,
                    blocked: false,
                    withoutContext: context != null
                );
            }
            catch
            {
                // Silently handle any errors to avoid affecting the original LLM call
                LogHelper.ErrorLog(Agent.Logger, "Error tracking LLM call statistics.");
            }
        }

        /// <summary>
        /// Extracts the cloud provider name
        /// </summary>
        /// <param name="searchString">The search string to extract the provider from.</param>
        /// <param name="provider">The extracted provider name.</param>
        /// <returns>True if the provider was extracted successfully, false otherwise. Not being used at the moment.</returns>
        internal static bool TryGetCloudProvider(string searchString, out string provider)
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
