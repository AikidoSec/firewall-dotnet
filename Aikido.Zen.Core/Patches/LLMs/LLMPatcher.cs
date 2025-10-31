using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Patches.LLMs.LLMResultParsers;
using Aikido.Zen.Core.Patches.LLMs.LLMResultParsers.Abstractions;

[assembly: InternalsVisibleTo("Aikido.Zen.Test")]

namespace Aikido.Zen.Core.Patches.LLMs
{
    /// <summary>
    /// Patches for LLM client operations to track and monitor LLM API calls
    /// </summary>
    public static class LLMPatcher
    {

        private static readonly List<ILLMResponseHandler> _responseHandlers = new List<ILLMResponseHandler>()
        {
            new OpenAIResponseHandler(),
            new AwsBedrockHandler(),
            new RystemOpenAIResponseHandler(),
            new GenericResponseHandler()
        };

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
                //If we are patching an async method, we need to get the result from the Task
                if (result is Task task)
                {
                    var resultProperty = task.GetType().GetProperty("Result");
                    if (resultProperty != null)
                    {
                        var taskResult = resultProperty.GetValue(task);
                        result = taskResult;
                    }
                }

                foreach (var handler in _responseHandlers)
                    if (handler.CanHandle(assembly))
                    {
                        handler.Handle(result, assembly, __originalMethod, context);
                        return;
                    }
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
