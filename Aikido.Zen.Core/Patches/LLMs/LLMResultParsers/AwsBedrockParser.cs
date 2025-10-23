using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models.LLMs;
using Aikido.Zen.Core.Models.LLMs.Sinks;
using System;
using System.Linq;

namespace Aikido.Zen.Core.Patches.LLMs.LLMResultParsers
{
    internal class AwsBedrockParser : BaseResponseParser
    {
        public override bool CanParse(string assembly) => assembly.Contains(LLMSinks.Sinks.First(s => s.Provider == LLMProviderEnum.AwsBedrock).Assembly);


        /// <summary>
        /// Parses the model name of the LLM model from the LLM result object.
        /// </summary>
        /// <param name="result">The output result object from the LLM API</param>
        /// <param name="assembly">The assembly of the patched method which generated the response</param>
        /// <returns></returns>
        /// <param name="method"></param>
        protected override string ParseModelName(object result, string assembly, string method)
        {
            switch (method)
            {
                case "Converse":
                case "ConverseAsync":
                    return ParseConverseModelName(result, assembly);
                case "ConverseStream":
                case "ConverseStreamAsync":
                    return ParseConverseStreamModelName(result, assembly);
                case "InvokeModel":
                case "InvokeModelAsync":
                    return ParseInvokeMethodModelName(result, assembly);
                case "InvokeModelWithResponseStream":
                case "InvokeModelWithResponseStreamAsync":
                    return ParseInvokeMethodWithResponseStreamModelName(result, assembly);
                default:
                    LogHelper.ErrorLog(Agent.Logger, $"Aws Bedrock Parser does not support method: {method}");
                    return "unknown";
            }
        }

        /// <summary>
        /// Prases the token usage from the LLM result object.
        /// </summary>
        /// <param name="result">The output result object from the LLM API</param>
        /// <param name="assembly">The assembly of the patched method which generated the response</param>
        /// <returns>Token usage object which contains the number of Input and Output tokens used.</returns>
        protected override TokenUsage ParseTokenUsage(object result, string assembly, string method)
        {
            var awsSink = LLMSinks.Sinks.First(LLMSink => LLMSink.Provider == LLMProviderEnum.AwsBedrock);

            awsSink.Methods.Select(m => m.Name).ToList();

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

        private string ParseConverseModelName(object result, string assembly)
        {
            var modelName = "unknown";
            return modelName;
        }
        private string ParseConverseStreamModelName(object result, string assembly)
        {
            var modelName = "unknown";
            return modelName;
        }
        private string ParseInvokeMethodModelName(object result, string assembly)
        {
            var modelName = "unknown";
            return modelName;
        }
        private string ParseInvokeMethodWithResponseStreamModelName(object result, string assembly)
        {
            var modelName = "unknown";
            return modelName;
        }
    }
}
