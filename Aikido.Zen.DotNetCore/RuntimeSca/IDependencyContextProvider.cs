using Microsoft.Extensions.DependencyModel;

namespace Aikido.Zen.DotNetCore.RuntimeSca
{
    internal interface IDependencyContextProvider
    {
        IEnumerable<RuntimeLibrary> GetRuntimeLibraries();
    }
}
