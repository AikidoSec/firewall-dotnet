namespace Aikido.Zen.Core.Patches.LLMs.LLMResultParsers
{
    /// <summary>
    /// Fallback parser that attempts to parse any LLM response when no specific parser is available.
    /// </summary>
    internal sealed class GenericResponseParser : BaseResponseParser
    {
        public override bool CanParse(string assembly) => true; // Fallback parser as a best effort in case no other is matched
    }
}
