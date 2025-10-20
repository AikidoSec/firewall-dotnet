using System.Collections.Generic;

namespace Aikido.Zen.Core.Models.LLMs.Sinks
{
    /// <summary>
    /// Represents an LLM sink for a specific provider which holds the methods of that provider that we want to patch
    /// </summary>
    internal class LLMSink
    {
        internal LLMSink(string assembly, IList<LLMMethod> methods)
        {
            Assembly = assembly;
            Methods = new List<LLMMethod>(methods).AsReadOnly();
        }

        internal LLMProviderEnum Provider { get; set; }
        internal string Assembly { get; private set; }

        internal IReadOnlyCollection<LLMMethod> Methods { get; private set; }
    }
}
