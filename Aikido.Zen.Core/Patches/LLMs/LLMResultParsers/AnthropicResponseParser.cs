using System.Linq;

using Aikido.Zen.Core.Models.LLMs.Sinks;

namespace Aikido.Zen.Core.Patches.LLMs.LLMResultParsers
{
    /// <summary>
    /// Parses LLM response from OpenAI model into <see cref="ParsedLLMResponseModel"/> instance.
    /// </summary>
    internal class AnthropicResponseParser : BaseResponseParser
    {
        public override bool CanParse(string assembly) => assembly.Contains(LLMSinks.Sinks.First(s => s.Provider == LLMProviderEnum.Anthropic).Assembly);
    }
}
