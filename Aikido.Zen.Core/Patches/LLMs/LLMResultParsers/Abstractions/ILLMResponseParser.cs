using Aikido.Zen.Core.Models.LLMs;

namespace Aikido.Zen.Core.Patches.LLMs.LLMResultParsers.Abstractions
{
    internal interface ILLMResponseParser
    {
        bool CanParse(string assembly);
        ParsedLLMResponseModel Parse(object result, string assembly, string method);
    }
}
