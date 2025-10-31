using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models.LLMs;
using Aikido.Zen.Core.Models.LLMs.Sinks;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;

namespace Aikido.Zen.Core.Patches.LLMs.LLMResultParsers
{
    internal class AwsBedrockHandler : BaseResponseHandler
    {
        public override bool CanHandle(string assembly) => assembly.Contains(LLMSinks.Sinks.First(s => s.Provider == LLMProviderEnum.AwsBedrock).Assembly);

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
                case "InvokeModel":
                case "InvokeModelAsync":
                    return ParseInvokeMethodModelName(result, assembly);
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
            switch (method)
            {
                case "Converse":
                case "ConverseAsync":
                    return ParseConverseTokenUsage(result, assembly);                   
                case "InvokeModel":
                case "InvokeModelAsync":
                    return ParseInvokeMethodTokenUsage(result, assembly);
                default:
                    LogHelper.ErrorLog(Agent.Logger, $"Aws Bedrock Parser does not support method: {method}");
                    return new TokenUsage();
            }
        }

        private string ParseConverseModelName(object result, string assembly)
        {
            var modelName = "unknown";

            var trace = result.GetType().GetProperty("Trace", bindingFlags)?.GetValue(result);
            if (trace is null)
            {
                LogHelper.ErrorLog(Agent.Logger, $"LLM Model Name Parsing failed from the assembly: {assembly} Reason: Failed to retrieve the Trace property.");
                modelName = "Not provided";
                return modelName;
            }

            var promptRouter = trace.GetType().GetProperty("PromptRouter", bindingFlags)?.GetValue(trace);
            if (promptRouter is null)
            {
                LogHelper.ErrorLog(Agent.Logger, $"LLM Model Name Parsing failed from the assembly: {assembly} Reason: Failed to retrieve the Prompt Router property.");
                modelName = "Not provided";
                return modelName;
            }

            var invokedModelId = promptRouter.GetType().GetProperty("InvokedModelId", bindingFlags);
            if(invokedModelId is null)
            {
                LogHelper.ErrorLog(Agent.Logger, $"LLM Model Name Parsing failed from the assembly: {assembly} Reason: Failed to retrieve the InvokedModelId property.");
                modelName = "Not provided";
                return modelName;
            }

            return invokedModelId.GetValue(promptRouter)?.ToString() ?? modelName;
        }
        private string ParseInvokeMethodModelName(object result, string assembly)
        {
            var modelName = "unknown";

            var resultType = result.GetType();
            var bodyProperty = resultType.GetProperty("Body", bindingFlags);
            var bodyValue = bodyProperty?.GetValue(result);
            if (bodyValue is null)
            {
                LogHelper.ErrorLog(Agent.Logger, $"LLM Token Usage Parsing failed from the assembly: {assembly} Reason: Failed to retrieve the Body object.");
                return modelName;
            }

            if (bodyValue is MemoryStream stream)
            {
                stream.Position = 0;
                using (var doc = JsonDocument.Parse(stream))
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("model", out var model))                    
                        modelName = model.GetString() ?? modelName;
                    
                }
                stream.Position = 0;
            }

            return modelName;
        }

        private TokenUsage ParseConverseTokenUsage(object result, string assembly)
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
                var inputTokens = usageType.GetProperty("InputTokens", bindingFlags);
                if (inputTokens is null)
                    LogHelper.ErrorLog(Agent.Logger, $"LLM Token Usage Parsing failed from the assembly: {assembly} Reason: Failed to retrieve the InputTokens property.");
                else
                    tokenUsage.InputTokens = Convert.ToInt64(inputTokens.GetValue(usageObj));

                // Output Tokens
                var outputTokens = usageType.GetProperty("OutputTokens", bindingFlags);
                if (outputTokens is null)
                    LogHelper.ErrorLog(Agent.Logger, $"LLM Token Usage Parsing failed from the assembly: {assembly} Reason: Failed to retrieve the OutputTokens property.");
                else
                    tokenUsage.OutputTokens = Convert.ToInt32(outputTokens.GetValue(usageObj));

                return tokenUsage;
            }
            catch (Exception e)
            {
                LogHelper.ErrorLog(Agent.Logger, $"LLM Token Usage Parsing failed from the assembly: {assembly} Reason: {e.Message}");
            }
            return new TokenUsage();
        }

        private TokenUsage ParseInvokeMethodTokenUsage(object result, string assembly)
        {
            var tokenUsage = new TokenUsage();
            var resultType = result.GetType();
            var bodyProperty = resultType.GetProperty("Body", bindingFlags);
            var bodyValue = bodyProperty?.GetValue(result);
            if(bodyValue is null)
            {
                LogHelper.ErrorLog(Agent.Logger, $"LLM Token Usage Parsing failed from the assembly: {assembly} Reason: Failed to retrieve the Body object.");
                return new TokenUsage();
            }

            if(bodyValue is MemoryStream stream)
            {
                stream.Position = 0;
                using (var doc = JsonDocument.Parse(stream))
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("usage", out var usage))
                    {
                        if (usage.TryGetProperty("input_tokens", out var it))
                        { tokenUsage.InputTokens = it.GetInt64(); }
                        if (usage.TryGetProperty("output_tokens", out var ot))
                        { tokenUsage.OutputTokens = ot.GetInt64(); }
                    }
                }
                stream.Position = 0;
            }

            return tokenUsage;
        }

        /// <summary>
        /// Prases the LLM result object from a streaming method.
        /// </summary>
        /// <param name="result">The result of the LLM request</param>
        /// <param name="assembly">The assembly from which the call originated</param>
        /// <param name="method">Calling method from which the call originated</param>
        /// <param name="context">Zen context used to report stats</param>
        /// <param name="stopwatch">Stopwatch measuring total duration</param>
        protected override void ParseStream(object result, string assembly, MethodBase method, Context context, Stopwatch stopwatch)
        {
            ParseConverseStream(result, assembly, method, context, stopwatch);
        }

        /// <summary>
        /// Attaches an event handler to the ConverseStreamResponse to capture token usage from streaming responses.
        /// </summary>
        private void ParseConverseStream(object result, string assembly, MethodBase method, Context context, Stopwatch stopwatch)
        {
            try
            {
                var pubInst = BindingFlags.Public | BindingFlags.Instance;

                // If result is ConverseStreamResponse, get its Stream (ConverseStreamOutput)
                var streamObj = result.GetType().GetProperty("Stream", pubInst)?.GetValue(result) ?? result;

                var evt = streamObj.GetType().GetEvent("MetadataReceived", pubInst);
                if (evt == null)
                {
                    LogHelper.ErrorLog(Agent.Logger, $"LLM Token Usage Parsing failed from the assembly: {assembly} Reason: Failed to retrieve the MetadataReceived event.");
                    return;
                }

                // Build a delegate for the event handler
                var handlerType = evt.EventHandlerType;
                var invoke = handlerType.GetMethod("Invoke");
                //Retrieves the parameters of the delegate
                var parameters = invoke.GetParameters();
                var s = Expression.Parameter(parameters[0].ParameterType, "s");
                var e = Expression.Parameter(parameters[1].ParameterType, "e");
                //Retrieves the method info of the handler so that we can call it
                var handler = typeof(AwsBedrockHandler).GetMethod(nameof(OnConverseStreamMetadataReceived), BindingFlags.NonPublic | BindingFlags.Instance);
                //Gets the EventStreamEvent property from the event args which contains the actual payload
                var getEventPayloadProperty = e.Type.GetProperty("EventStreamEvent", pubInst);
                var eventPayloadExpression = getEventPayloadProperty != null
                    ? Expression.Convert(Expression.Property(e, getEventPayloadProperty), typeof(object))
                    : Expression.Convert(e, typeof(object));

                //Builds the method call expression which will call our handler
                var body = Expression.Call(
                    Expression.Constant(this),
                    handler,
                    eventPayloadExpression,
                    Expression.Constant(assembly, typeof(string)),
                    Expression.Constant(method, typeof(MethodBase)),
                    Expression.Constant(context, typeof(Context)),
                    Expression.Constant(stopwatch, typeof(Stopwatch))
                );

                //Compile the expression into a delegate and attach it to the event
                var handlerDelegate = Expression.Lambda(handlerType, body, s, e).Compile();
                evt.AddEventHandler(streamObj, handlerDelegate);
            }
            catch (Exception ex)
            {
                LogHelper.ErrorLog(Agent.Logger, $"LLM Stream parsing failed from the assembly: {assembly} Reason: {ex.Message}");
            }
        }

        /// <summary>
        /// Event handler for ConverseStreamResponse MetadataReceived event to capture token usage and Model name.
        /// </summary>
        private void OnConverseStreamMetadataReceived(object eventPayload, string assembly, MethodBase method, Context context, Stopwatch stopwatch)
        {
            //The incoming event paylod is of type ConverseStreamOutput but has the same structure as the Converse response
            var parsedResponse = new ParsedLLMResponseModel
            {
                Model = ParseConverseModelName(eventPayload, assembly),
                TokenUsage = ParseConverseTokenUsage(eventPayload, assembly)
            };

            ReportStats(parsedResponse, assembly, context, method, stopwatch);
        }
    }
}
