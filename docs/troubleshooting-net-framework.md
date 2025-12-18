# Troubleshooting .NET Framework assembly loading
This troubleshooting guide is targeted at .NET Framework users dealing with the following error:

```
Could not load file or assembly 'Aikido.Zen.Core, Version=1.2.27.0, Culture=neutral, PublicKeyToken=null' or one of its dependencies. The system cannot find the file specified.
```

.NET Framework assemblies usually resolve from Nuget folders or the GAC.

You can inspect attempted load paths using [Process Monitor](https://learn.microsoft.com/en-us/sysinternals/downloads/procmon) (straightforward to use) or [Fusion Log Viewer](https://learn.microsoft.com/en-us/dotnet/framework/tools/fuslogvw-exe-assembly-binding-log-viewer) (more advanced, but requires custom configuration)

There are 2 main Nuget locations, depending on the package management format:
- **packages.config file**: $(SolutionDir)\packages\
- **PackageReference .csproj**: %USERPROFILE%\\.nuget\packages

The GAC is a machine wide directory (C:\Windows\Microsoft.NET\assembly) where assemblies are installed.

`gacutil` can be used from the VS Developer Command Prompt to list, install or remove assemblies.

## Adding an explicit assembly path to .csproj
Use the `HintPath` tag to point references to the correct location (adjust the version and target framework to match your project).

```
<Reference Include="Aikido.Zen.Core, Version=1.2.27.0, Culture=neutral, PublicKeyToken=null">
  <HintPath>..\..\packages\Aikido.Zen.DotNetFramework.1.2.27\lib\net48\Aikido.Zen.Core.dll</HintPath>
</Reference>
<Reference Include="Aikido.Zen.DotNetFramework, Version=1.2.27.0, Culture=neutral, PublicKeyToken=null">
  <HintPath>..\..\packages\Aikido.Zen.DotNetFramework.1.2.27\lib\net48\Aikido.Zen.DotNetFramework.dll</HintPath>
</Reference>
```

## Nuget / dotnet / msbuild package paths
- `nuget.exe restore` with `packages.config`: `$(SolutionDir)\packages\Aikido.Zen.DotNetFramework.<version>\lib\<tfm>\`. (The `<tfm>` is `net461`, `net47`, or `net48`.)
- `dotnet restore` or `msbuild /restore` with `PackageReference`: `%USERPROFILE%\.nuget\packages\aikido.zen.dotnetframework\<version>\lib\<tfm>\` unless overridden by `NUGET_PACKAGES` or `RestorePackagesPath`.
- Custom `nuget.config` values (`repositoryPath` or `globalPackagesFolder`) or `nuget.exe restore -PackagesDirectory` can redirect the package folder; check `nuget locals global-packages -list` to confirm the active location.
- Hosted build agents (Azure DevOps/GitHub): packages land under the agent profile, e.g., `C:\Users\vstsagent\.nuget\packages\...`.

## Tips
- Keep `HintPath` values relative so the solution stays portable across machines.
- Align the `HintPath` target framework with the project target (e.g., use `net461`/`net47`/`net48`).
- When bumping versions, update both the `Include` version and the `HintPath` folder names.
