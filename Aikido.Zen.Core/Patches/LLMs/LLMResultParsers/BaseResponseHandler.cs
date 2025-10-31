using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models.LLMs;
using Aikido.Zen.Core.Patches.LLMs.LLMResultParsers.Abstractions;
using System;
using System.Diagnostics;
using System.Reflection;

namespace Aikido.Zen.Core.Patches.LLMs.LLMResultParsers
{
    /// <summary>
    /// Base class for parsing LLM responses into <see cref="ParsedLLMResponseModel"/> instances.
    /// Provides basic implementations for extracting model name and token usage
    /// </summary>
    internal abstract class BaseResponseHandler : ILLMResponseHandler
    {
        protected readonly string operationKind = "ai_op";
        protected const BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;
        public abstract bool CanHandle(string assembly);

        /// <summary>
        /// Determines whether the specified method name contains the word "stream",  using a case-insensitive
        /// comparison.
        /// </summary>
        /// <param name="method">The method name to evaluate. Can be null.</param>
        /// <returns><see langword="true"/> if the specified method name contains the word "stream";  otherwise, <see
        /// langword="false"/>.</returns>
        public virtual bool IsStreamMethod(string method) { return method?.IndexOf("stream", StringComparison.OrdinalIgnoreCase) >= 0; }

        /// <summary>
        /// Parses the incoming LLM result into a <see cref="ParsedLLMResponseModel"/> instance.
        /// </summary>
        /// <param name="result">The result of the LLM request</param>
        /// <param name="assembly">The assembly from which the call originated</param>
        /// <param name="method">Calling method from which the call originated</param>
        /// <returns>Parsed <see cref="ParsedLLMResponseModel"/>, or an empty model.</returns>
        public virtual void Handle(object result, string assembly, MethodBase method, Context context)
        {
            var stopwatch = Stopwatch.StartNew();

            if (context is null)
                LogHelper.ErrorLog(Agent.Logger, $"OnLLMHandle: Context is null. Provider: {assembly} Method: {method}");

            if (result == null)            
                LogHelper.ErrorLog(Agent.Logger, $"OnLLMHandle: Result is null. Provider: {assembly} Method: {method}");    

            if(IsStreamMethod(method.Name))
            {
                ParseStream(result, assembly, method, context, stopwatch);
                return;
            }

            var parsedResponse = new ParsedLLMResponseModel
            {
                Model = ParseModelName(result, assembly, method.Name),
                TokenUsage = ParseTokenUsage(result, assembly, method.Name)
            };

            ReportStats(parsedResponse, assembly, context, method, stopwatch);
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

        /// <summary>
        /// Prases the LLM result object from a streaming method.
        /// </summary>
        /// <param name="result">The result of the LLM request</param>
        /// <param name="assembly">The assembly from which the call originated</param>
        /// <param name="method">Calling method from which the call originated</param>
        protected virtual void ParseStream(object result, string assembly, MethodBase method, Context context, Stopwatch stopwatch)
        {
            LogHelper.ErrorLog(Agent.Logger, $"LLM Stream Parsing attempted using Base Handler for the Assembly: {assembly}, Method: {method}");
        }

        protected void ReportStats(ParsedLLMResponseModel parsedResponse, string assembly, Context context, MethodBase method, Stopwatch stopwatch)
        {
            // Record AI statistics
            Agent.Instance.Context.OnAiCall(assembly, parsedResponse.Model, parsedResponse.TokenUsage.InputTokens, parsedResponse.TokenUsage.OutputTokens, context?.Route);

            // record sink statistics
            Agent.Instance.Context.OnInspectedCall(
                operation: $"{method.DeclaringType.Namespace}.{method.DeclaringType.Name}.{method.Name}",
                kind: operationKind,
                durationInMs: stopwatch.ElapsedMilliseconds,
                attackDetected: false,
                blocked: false,
                withoutContext: context == null
            );
        }
    }
}
