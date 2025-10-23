using System.Collections.Generic;
using System.Threading.Tasks;

using Aikido.Zen.Core.Models.LLMs;
using Aikido.Zen.Core.Patches.LLMs.LLMResultParsers;
using Aikido.Zen.Core.Patches.LLMs.LLMResultParsers.Abstractions;

namespace Aikido.Zen.Core.Patches.LLMs
{
    /// <summary>
    /// Resolves and applies the appropriate parser to process a response object from an LLM.
    /// </summary>
    /// <remarks>This class maintains a collection of response parsers and determines the correct parser to
    /// use based on the  provided result object and assembly name. If the result is a <see cref="Task"/>, it waits for
    /// the task to  complete and extracts the result before attempting to parse it.</remarks>
    internal static class LLMResponseParserResolver
    {
        private static readonly List<ILLMResponseParser> _parsers = new List<ILLMResponseParser>();

        /// <summary>
        /// Adds the available parsers to the resolver.
        /// Contains a Generic parser as a fallback, which should always be the last one in the list
        /// since we itterate over it.
        /// </summary>
        static LLMResponseParserResolver()
        {
            _parsers.Add(new OpenAIResponseParser());
            _parsers.Add(new AzureOpenAIParser());
            _parsers.Add(new AwsBedrockParser());
            _parsers.Add(new AnthropicResponseParser());
            _parsers.Add(new RystemOpenAIResponseParser());
            _parsers.Add(new GenericResponseParser());
        }

        /// <summary>
        /// Entry for parsing LLM responses. Chooses the correct parser based on the individual parser's CanParse method and assembly name.
        /// It also checks if the result is a Task and extracts the result if it is.
        /// </summary>
        /// <param name="result">The result of the LLM request</param>
        /// <param name="assembly">The assembly from which the call originated</param>
        /// <param name="method">Calling method from which the call originated</param>
        /// <returns>Parsed response or an empty model if the parsing failed</returns>
        internal static ParsedLLMResponseModel Parse(object result, string assembly, string method)
        {
            //If we are patching an async method, we need to get the result from the Task
            if (result is Task task)
            {
                var resultProperty = task.GetType().GetProperty("Result");
                if (resultProperty != null)
                {
                    var taskResult = resultProperty.GetValue(task);
                    var resultType = taskResult.GetType();
                    var usageProp = resultType.GetProperty("Value");
                    result = usageProp?.GetValue(taskResult);
                }
            }

            foreach (var parser in _parsers)            
                if (parser.CanParse(assembly))
                    return parser.Parse(result, assembly, method);
                
            return new ParsedLLMResponseModel();
        }
    }
}
