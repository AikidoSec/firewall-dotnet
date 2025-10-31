using Aikido.Zen.Core.Models.LLMs;
using System.Reflection;

namespace Aikido.Zen.Core.Patches.LLMs.LLMResultParsers.Abstractions
{
    internal interface ILLMResponseHandler
    {
        bool CanHandle(string assembly);
        bool IsStreamMethod(string method);
        void Handle(object result, string assembly, MethodBase method, Context context);
    }
}
