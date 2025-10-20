namespace Aikido.Zen.Core.Patches.LLMs.LLMResultParsers
{
    /// <summary>
    /// Fallback parser that attempts to parse any LLM response when no specific parser is available.
    /// </summary>
    internal sealed class GenericResponseParser : BaseResponseParser
    {
        public override bool CanParse(object result, string assembly) => true; // Always returns true as a fallback parser
    }
}
