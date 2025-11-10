using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

using Aikido.Zen.Core.Helpers;

[assembly: InternalsVisibleTo("Aikido.Zen.Test")]

namespace Aikido.Zen.Core.Patches.LLMs
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
            // Exclude certain assemblies to avoid stack overflow issues
            if (ReflectionHelper.ShouldSkipAssembly())            
                return;           

            try
            {
                var stopWatch = Stopwatch.StartNew();

                if (context is null)
                    LogHelper.ErrorLog(Agent.Logger, "OnLLMCallCompleted: Context is null.");

                var parsedResponse = LLMResponseParserResolver.Parse(result, assembly);

                // Record AI statistics
                Agent.Instance.Context.OnAiCall(assembly, parsedResponse.Model, parsedResponse.TokenUsage.InputTokens, parsedResponse.TokenUsage.OutputTokens, context?.Route);

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
    }
}
