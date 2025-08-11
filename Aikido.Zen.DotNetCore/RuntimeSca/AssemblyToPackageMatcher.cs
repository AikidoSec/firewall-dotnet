using Microsoft.Extensions.DependencyModel;
using System.Diagnostics;
using System.Reflection;

namespace Aikido.Zen.DotNetCore.RuntimeSca
{
    internal class AssemblyToPackageMatcher
    {
        private readonly IDependencyContextProvider _dependencyContextProvider;
        private readonly IFileVersionInfoProvider _fileVersionInfoProvider;

        internal AssemblyToPackageMatcher(
            IDependencyContextProvider dependencyContextProvider,
            IFileVersionInfoProvider fileVersionInfoProvider)
        {
            _dependencyContextProvider = dependencyContextProvider;
            _fileVersionInfoProvider = fileVersionInfoProvider;
        }

        internal RuntimeLibrary FindLibraryForAssembly(Assembly assembly)
        {
            if (string.IsNullOrEmpty(assembly?.Location))
            {
                return null;
            }

            var assemblyFileName = Path.GetFileName(assembly.Location);
            var assemblyVersion = assembly.GetName()?.Version?.ToString();
            var fileVersion = GetFileVersion(assembly.Location);

            foreach (var library in _dependencyContextProvider.GetRuntimeLibraries())
            {
                foreach (var group in library.RuntimeAssemblyGroups)
                {
                    foreach (var file in group.RuntimeFiles)
                    {
                        if (!Path.GetFileName(file.Path).Equals(assemblyFileName, StringComparison.OrdinalIgnoreCase))
                        {
                            // The assembly file name (eg: "System.Runtime.dll") must match the file name in the runtime files.
                            continue;
                        }

                        if (!assemblyVersion.Equals(file.AssemblyVersion, StringComparison.OrdinalIgnoreCase))
                        {
                            // The assembly version (eg: "1.0.0.0") must match the file version in the runtime files.
                            continue;
                        }

                        if (fileVersion != null &&
                            !fileVersion.Equals(file.FileVersion, StringComparison.OrdinalIgnoreCase))
                        {
                            // If there is a file version, it must match the file version in the runtime files.
                            // If there is no file version, it is safe to skip this check.
                            continue;
                        }

                        return library;
                    }
                }
            }

            return null;
        }

        private string GetFileVersion(string filePath)
        {
            FileVersionInfo fileVersionInfo;
            try
            {
                fileVersionInfo = _fileVersionInfoProvider.GetVersionInfo(filePath);
            }
            catch (Exception)
            {
                // If we can't get file version info, we can't match the library
                return null;
            }

            // Getting the fileVersionInfo.FileVersion omits the ending '.0' in some cases.
            // This is part of the {application}.deps.json file though.
            // So we need it to match the correct assembly with the correct package.
            return $"{fileVersionInfo.FileMajorPart}.{fileVersionInfo.FileMinorPart}.{fileVersionInfo.FileBuildPart}.{fileVersionInfo.FilePrivatePart}";
        }
    }
}
