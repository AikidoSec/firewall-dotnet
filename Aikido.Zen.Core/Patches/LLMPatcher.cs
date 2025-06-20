using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;

namespace Aikido.Zen.Core.Patches
{
    /// <summary>
    /// Patches for LLM client operations to track and monitor LLM API calls
    /// </summary>
    public static class LLMPatcher
    {
        private const string operationKind = "llm_op";

        /// <summary>
        /// Patches the OnLLMCallExecuting method to track and monitor LLM API calls
        /// </summary>
        /// <param name="__args">The arguments passed to the method.</param>
        /// <param name="__originalMethod">The original method being patched.</param>
        /// <param name="messages">The chat messages being sent to the LLM.</param>
        /// <param name="assembly">The assembly name containing the LLM client.</param>
        /// <param name="context">The current Aikido context.</param>
        /// <returns>True to allow the original method to execute, false to block it.</returns>
        public static bool OnLLMCallExecuting(object[] __args, MethodBase __originalMethod, object messages, string assembly, Context context)
        {
            // TODO: Implement LLM call monitoring
            return true;
        }

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
                if (context == null || result == null) return;


                if (!TryExtractModelFromResult(result, out var model))
                {
                    LogHelper.ErrorLog(Agent.Logger, $"Failed to extract model from LLM result for model: {model}");
                }

                if (!TryGetProvider($"{model} {assembly} {result.GetType().ToString()}", out var provider))
                {
                    LogHelper.ErrorLog(Agent.Logger, $"Failed to extract provider from LLM for model: {model}, provider: {provider}");
                }

                if (!TryExtractTokensFromResult(result, out var tokens))
                {
                    LogHelper.ErrorLog(Agent.Logger, $"Failed to extract token usage from LLM result for provider: {provider}, model: {model}");
                }

                // Record AI statistics
                Agent.Instance.Context.OnAiCall(provider, model, tokens.inputTokens, tokens.outputTokens, context.Route);
            }
            catch
            {
                // Silently handle any errors to avoid affecting the original LLM call
                try { LogHelper.ErrorLog(Agent.Logger, "Error tracking LLM call statistics."); } catch {/*ignore*/}
            }
        }

        /// <summary>
        /// Extracts the provider name
        /// </summary>
        private static bool TryGetProvider(string searchString, out string provider)
        {
            provider = "unknown";
            searchString = searchString.ToLower();
            // first the cloud providers
            if (searchString.Contains("azure"))
            {
                provider = "azure";
                return true;
            }
            // than the llm companies
            if (searchString.Contains("anthropic") || searchString.Contains("claude"))
            {
                provider = "anthropic";
                return true;
            }
            if (searchString.Contains("google") || searchString.Contains("gemini"))
            {
                provider = "google";
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
        /// Converts a dynamic object to a dictionary using reflection
        /// </summary>
        private static Dictionary<string, object> ConvertObjectToDictionary(object obj)
        {
            var dictionary = new Dictionary<string, object>();

            if (obj == null) return dictionary;

            // Handle if it's already an IDictionary
            if (obj is IDictionary<string, object> existingDict)
            {
                return new Dictionary<string, object>(existingDict);
            }

            // Use reflection to get all properties
            var type = obj.GetType();
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                try
                {
                    var value = property.GetValue(obj);
                    dictionary[property.Name] = value;
                }
                catch
                {
                    // Skip properties that can't be accessed
                }
            }

            return dictionary;
        }

        /// <summary>
        /// Extracts the model name from the result based on the provider
        /// </summary>
        private static bool TryExtractModelFromResult(object result, out string model)
        {
            model = "unknown";
            try
            {
                var resultAsDictionary = ConvertObjectToDictionary(result);
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
                var resultAsDictionary = ConvertObjectToDictionary(result);
                if (resultAsDictionary.Count == 0)
                {
                    tokens = (0, 0);
                    return false;
                }

                if (resultAsDictionary.TryGetValue("Usage", out object usage))
                {
                    var usageAsDictionary = ConvertObjectToDictionary(usage);
                    long? inputTokens = null;
                    long? outputTokens = null;

                    // OpenAI / Azure OpenAI client
                    if (usageAsDictionary.TryGetValue("InputTokenCount", out var input))
                        inputTokens = Convert.ToInt64(input);
                    // Rystem.OpenAi client
                    else if (usageAsDictionary.TryGetValue("PromptTokens", out var prompt))
                        inputTokens = Convert.ToInt64(prompt);

                    // OpenAI / Azure OpenAI client
                    if (usageAsDictionary.TryGetValue("OutputTokenCount", out var output))
                        outputTokens = Convert.ToInt64(output);
                    // Rystem.OpenAi client
                    else if (usageAsDictionary.TryGetValue("CompletionTokens", out var completion))
                        outputTokens = Convert.ToInt64(completion);

                    tokens = (inputTokens ?? 0, outputTokens ?? 0);
                    return true;
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
