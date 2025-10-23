using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Aikido.Zen.Core.Models.LLMs.Sinks
{
    internal sealed class LLMMethod
    {
        internal string Name { get; }
        internal string Type { get; }
        internal IReadOnlyList<string> Parameters { get; }

        public LLMMethod(string name, string type, string[] methodParameters)
        {
            Name = name;
            Type = type;
            Parameters = new ReadOnlyCollection<string>(methodParameters);
        }
    }
}
