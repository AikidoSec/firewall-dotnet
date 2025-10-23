using System;
using System.Reflection;

using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models.LLMs;
using Aikido.Zen.Core.Patches.LLMs.LLMResultParsers.Abstractions;

namespace Aikido.Zen.Core.Patches.LLMs.LLMResultParsers
{
    /// <summary>
    /// Base class for parsing LLM responses into <see cref="ParsedLLMResponseModel"/> instances.
    /// Provides basic implementations for extracting model name and token usage
    /// </summary>
    internal abstract class BaseResponseParser : ILLMResponseParser
    {
        protected const BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;
        public abstract bool CanParse(string assembly);

        /// <summary>
        /// Parses the incoming LLM result into a <see cref="ParsedLLMResponseModel"/> instance.
        /// </summary>
        /// <param name="result">The result of the LLM request</param>
        /// <param name="assembly">The assembly from which the call originated</param>
        /// <param name="method">Calling method from which the call originated</param>
        /// <returns>Parsed <see cref="ParsedLLMResponseModel"/>, or an empty model.</returns>
        public virtual ParsedLLMResponseModel Parse(object result, string assembly, string method)
        {
            if (result == null)
            {
                LogHelper.ErrorLog(Agent.Logger, $"LLM result is null for the provider: {assembly}");
                return new ParsedLLMResponseModel();
            }

            var parsedResponse = new ParsedLLMResponseModel();
            parsedResponse.Model = ParseModelName(result, assembly, method);
            parsedResponse.TokenUsage = ParseTokenUsage(result, assembly, method);

            return parsedResponse;
        }

        /// <summary>
        /// Parses the model name of the LLM model from the LLM result object.
        /// </summary>
        /// <param name="result">The result of the LLM request</param>
        /// <param name="assembly">The assembly from which the call originated</param>
        /// <param name="method">Calling method from which the call originated</param>
        /// <returns>Parsed model name, or "unknown"</returns>
        protected virtual string ParseModelName(object result, string assembly, string method)
        {
            var modelName = "unknown";
            try
            {
                var resultType = result.GetType();
                var modelProp = resultType.GetProperty("Model", bindingFlags);
                if (modelProp is null)
                    LogHelper.ErrorLog(Agent.Logger, $"LLM Model Name Parsing failed from the assembly: {assembly} Reason: Failed to retrieve the Model property.");
                else
                    modelName = modelProp.GetValue(result)?.ToString();
                return modelName;
            }
            catch (Exception e)
            {
                LogHelper.ErrorLog(Agent.Logger, $"LLM Model Name Parsing failed from the assembly: {assembly} Reason: {e.Message}");
            }
            return modelName;
        }

        /// <summary>
        /// Prases the token usage from the LLM result object.
        /// </summary>
        /// <param name="result">The result of the LLM request</param>
        /// <param name="assembly">The assembly from which the call originated</param>
        /// <param name="method">Calling method from which the call originated</param>
        /// <returns>Token usage object which contains the number of Input and Output tokens used.</returns>
        protected virtual TokenUsage ParseTokenUsage(object result, string assembly, string method)
        {
            var tokenUsage = new TokenUsage();
            try
            {
                var resultType = result.GetType();

                //Input Tokens
                var inputTokens = resultType.GetProperty("InputTokens", bindingFlags);
                if (inputTokens is null)
                    LogHelper.ErrorLog(Agent.Logger, $"LLM Token Usage Parsing failed from the assembly: {assembly} Reason: Failed to retrieve the Input Tokens property.");
                else
                    tokenUsage.InputTokens = Convert.ToInt64(inputTokens.GetValue(result));

                //Output Tokens
                var outputTokens = resultType.GetProperty("OutputTokens", bindingFlags);
                if (outputTokens is null)
                    LogHelper.ErrorLog(Agent.Logger, $"LLM Token Usage Parsing failed from the assembly: {assembly} Reason: Failed to retrieve the Output Tokens property.");
                else
                    tokenUsage.OutputTokens = Convert.ToInt64(outputTokens.GetValue(result));

                return tokenUsage;
            }
            catch (Exception e)
            {
                LogHelper.ErrorLog(Agent.Logger, $"LLM Token Usage Parsing failed from the assembly: {assembly} Reason: {e.Message}");
            }
            return tokenUsage;
        }
    }
}
