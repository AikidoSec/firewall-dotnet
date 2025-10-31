namespace Aikido.Zen.Core.Models.LLMs
{
    /// <summary>
    /// Parsed values from the completed LLM API call
    /// </summary>
    public class ParsedLLMResponseModel
    {
        internal string Model { get; set; } = "unknown";
        internal TokenUsage TokenUsage { get; set; }

    }

    /// <summary>
    /// Parsed token usage details from the LLM response
    /// </summary>
    internal class TokenUsage
    {
        internal long InputTokens { get; set; } = 0;
        internal long OutputTokens { get; set; } = 0;
    }
}
