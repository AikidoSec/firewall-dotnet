using System.Diagnostics;

namespace Aikido.Zen.DotNetCore.RuntimeSca
{
    internal interface IFileVersionInfoProvider
    {
        FileVersionInfo GetVersionInfo(string fileName);
    }
}
