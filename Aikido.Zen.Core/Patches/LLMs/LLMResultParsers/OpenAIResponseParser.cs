using System;
using System.Linq;

using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models.LLMs;
using Aikido.Zen.Core.Models.LLMs.Sinks;

namespace Aikido.Zen.Core.Patches.LLMs.LLMResultParsers
{
    /// <summary>
    /// Parses LLM response from OpenAI model into <see cref="ParsedLLMResponseModel"/> instance.
    /// </summary>
    internal sealed class OpenAIResponseParser : BaseResponseParser
    {
        public override bool CanParse(string assembly) => assembly.Contains(LLMSinks.Sinks.First(s => s.Provider == LLMProviderEnum.OpenAI).Assembly);
        /// <summary>
        /// Parses the incoming LLM result into a <see cref="ParsedLLMResponseModel"/> instance.
        /// </summary>
        /// <param name="result">The output result object from the LLM API</param>
        /// <param name="assembly">The assembly of the patched method which generated the response</param>
        /// <returns></returns>
        public override ParsedLLMResponseModel Parse(object result, string assembly)
        {
            result = UnwrapChatCompletion(result);

            return base.Parse(result, assembly);
        }

        /// <summary>
        /// Retrieves the token usage from the LLM result object. Specific to OpenAI structure.
        /// </summary>
        /// <param name="result">The output result object from the LLM API</param>
        /// <param name="assembly">The assembly of the patched method which generated the response</param>
        /// <returns>Token usage object which contains the number of Input and Output tokens used.</returns>
        protected override TokenUsage ParseTokenUsage(object result, string assembly)
        {
            try
            {
                var tokenUsage = new TokenUsage();
                // Usage
                var resultType = result.GetType();
                var usageProp = resultType.GetProperty("Usage", bindingFlags);
                var usageObj = usageProp?.GetValue(result);
                if (usageObj == null)
                {
                    LogHelper.ErrorLog(Agent.Logger, $"LLM Token Usage Parsing failed from the assembly: {assembly} Reason: Failed to retrieve the Usage object.");
                    return new TokenUsage();
                }

                // Input Tokens
                var usageType = usageObj.GetType();
                var inputTokens = usageType.GetProperty("InputTokenCount", bindingFlags);
                if(inputTokens is null)                
                    LogHelper.ErrorLog(Agent.Logger, $"LLM Token Usage Parsing failed from the assembly: {assembly} Reason: Failed to retrieve the InputTokenCount property.");
                else
                    tokenUsage.InputTokens = Convert.ToInt64(inputTokens.GetValue(usageObj));

                // Output Tokens
                var outputTokens = usageType.GetProperty("OutputTokenCount", bindingFlags);
                if(outputTokens is null)            
                    LogHelper.ErrorLog(Agent.Logger, $"LLM Token Usage Parsing failed from the assembly: {assembly} Reason: Failed to retrieve the OutputTokenCount property.");
                else
                    tokenUsage.OutputTokens = Convert.ToInt32(outputTokens.GetValue(usageObj));

                return tokenUsage;
            }
            catch(Exception e)
            {
                LogHelper.ErrorLog(Agent.Logger, $"LLM Token Usage Parsing failed from the assembly: {assembly} Reason: {e.Message}");
            }
            return new TokenUsage();
        }

        /// <summary>
        /// Unwraps the object from System.ClientModel.ClientResult<T> to get the actual result value
        /// </summary>
        /// <param name="result">Incoming response object</param>
        /// <returns>Unwrapped result object or null</returns>
        private static object UnwrapChatCompletion(object result)
        {
            var completionResult = result?.GetType();
            var valueProp = completionResult?.GetProperty("Value");
            return valueProp?.GetValue(result);
        }
    }
}
