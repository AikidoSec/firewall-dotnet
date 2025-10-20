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
    internal class LLMResponseParserResolver
    {
        private static readonly List<ILLMResponseParser> _parsers = new List<ILLMResponseParser>();

        static LLMResponseParserResolver()
        {
            _parsers.Add(new OpenAIResponseParser());
            _parsers.Add(new RystemOpenAIResponseParser());
            _parsers.Add(new GenericResponseParser());
        }

        internal static ParsedLLMResponseModel Parse(object result, string assembly)
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

            foreach (var p in _parsers)            
                if (p.CanParse(result, assembly))
                    return p.Parse(result, assembly);
                
            return new ParsedLLMResponseModel();
        }
    }
}
