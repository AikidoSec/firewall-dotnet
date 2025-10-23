using System.Linq;

using Aikido.Zen.Core.Models.LLMs.Sinks;

namespace Aikido.Zen.Core.Patches.LLMs.LLMResultParsers
{
    internal class AzureOpenAIParser : BaseResponseParser
    {
        public override bool CanParse(string assembly) => assembly.Contains(LLMSinks.Sinks.First(s => s.Provider == LLMProviderEnum.AzureOpenAI).Assembly);
    }
}
