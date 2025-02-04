#addin nuget:?package=Cake.FileHelpers&version=6.0.0
#load "nuget:https://www.nuget.org/api/v2?package=Cake.NuGet&version=5.0.0"



var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var framework = Argument("framework", "");
var solution = "./Aikido.Zen.sln";
var projectName = "Aikido.Zen.Core";
var zenInternalsVersion = "0.1.36";
var libVersion = Argument("libVersion", "0.0.1-alpha5");

var baseUrl = $"https://github.com/AikidoSec/zen-internals/releases/download/v{zenInternalsVersion}/";
var librariesDir = $"./{projectName}/libraries";

var filesToDownload = new string[] {
    "libzen_internals_aarch64-apple-darwin.dylib",
    "libzen_internals_aarch64-apple-darwin.dylib.sha256sum",
    "libzen_internals_aarch64-unknown-linux-gnu.so",
    "libzen_internals_aarch64-unknown-linux-gnu.so.sha256sum",
    "libzen_internals_x86_64-apple-darwin.dylib",
    "libzen_internals_x86_64-apple-darwin.dylib.sha256sum",
    "libzen_internals_x86_64-pc-windows-gnu.dll",
    "libzen_internals_x86_64-pc-windows-gnu.dll.sha256sum",
    "libzen_internals_x86_64-unknown-linux-gnu.so",
    "libzen_internals_x86_64-unknown-linux-gnu.so.sha256sum"
};

Task("Clean")
    .Does(() =>
    {
        CleanDirectories("./**/bin");
        CleanDirectories("./**/obj");
        CleanDirectory(librariesDir);
        Information("Clean task completed successfully.");
    });

Task("DownloadLibraries")
    .Does(() =>
    {
        EnsureDirectoryExists(librariesDir);
        // check if the same version is already downloaded
        var files = GetFiles($"{librariesDir}/**/*.sha256sum");
        if (files.Count > 0)
        {
            FilePath file = files.First();
            var currVersion = FileReadText(file).Split('-')[1];
            if (currVersion == zenInternalsVersion)
            {
                Information("Libraries already downloaded. skipping download.");
                return;
            }
        }

        foreach (var file in filesToDownload)
        {
            DownloadFile($"{baseUrl}{file}", $"{librariesDir}/{file}");
        }
        Information("DownloadLibraries task completed successfully.");
    });

Task("Restore")
    .Does(() =>
    {
        NuGetRestore(solution);
    });

Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("DownloadLibraries")
    .IsDependentOn("Restore")
    .Does(() =>
    {
        try
        {
            var msBuildSettings = new MSBuildSettings
            {
                Configuration = configuration,
                ToolVersion = MSBuildToolVersion.VS2022,
                Verbosity = Verbosity.Quiet,
                PlatformTarget = PlatformTarget.MSIL,
                MaxCpuCount = 1,
                DetailedSummary = false,
                NodeReuse = true
            }
            .WithTarget("Build")
            .WithProperty("version", libVersion);

            var projects = GetFiles("./**/*.csproj")
                .Where(p => !p.FullPath.Contains("sample-apps") && !p.FullPath.Contains("Aikido.Zen.Benchmarks"));

            foreach (var project in projects)
            {
                MSBuild(project, msBuildSettings);
            }
            Information("Build task completed successfully.");
        }
        catch (Exception ex)
        {
            Error($"Build failed with error: {ex.Message}");
            throw;
        }
    });

Task("Rebuild")
    .IsDependentOn("Clean")
    .IsDependentOn("Build");

Task("Test")
    .IsDependentOn("Build")
    .Does(() =>
    {
        var coverageDir = MakeAbsolute(Directory("./coverage"));
        EnsureDirectoryExists(coverageDir);

        // Get test projects from Aikido.Zen.Test directory
        var testProjects = GetFiles("./**/Aikido.Zen.Test*.csproj") as IEnumerable<FilePath>;
        foreach (var project in testProjects)
        {
            // skip tests for the wrong framework
            Information($"Running tests for {project.FullPath} on .NET Framework {framework}");
            if (framework.StartsWith("4.") && project.FullPath.Contains("DotNetCore"))
            {
                Information($"Skipping test project {project.FullPath} for .NET Framework {framework}");
                continue;
            }
            if (!framework.StartsWith("4.") && project.FullPath.Contains("DotNetFramework"))
            {
                Information($"Skipping test project {project.FullPath} for .NET Framework {framework}");
                continue;
            }

            var logFilePath = $"{coverageDir.FullPath}/test-results-{project.GetFilenameWithoutExtension()}.trx";

            DotNetTest(project.FullPath, new DotNetTestSettings
            {
                SetupProcessSettings = processSettings =>
                {
                    processSettings.RedirectStandardOutput = true;
                    processSettings.RedirectStandardError = true;
                },
                Configuration = configuration,
                NoBuild = true,
                NoRestore = true,
                ArgumentCustomization = args =>
                {
                    // for now, only collect coverage for the main tests project
                    if (project.FullPath.EndsWith("Aikido.Zen.Tests.csproj"))
                    {
                        args = args
                            .Append("/p:CollectCoverage=true")
                            .Append("/p:CoverletOutputFormat=opencover")
                            .Append($"/p:CoverletOutput={coverageDir.FullPath}/coverage.xml")
                            .Append("/p:Include=[Aikido.Zen.*]*")
                            .Append("/p:Exclude=[Aikido.Zen.Test]*");
                    }
                    // Increase verbosity to diagnostic for more information
                    return args
                        .Append("--verbosity diagnostic")
                        .Append($"--logger trx;LogFileName={logFilePath}");
                }
            });
        }
        Information($"Test task completed successfully. Coverage report at: {coverageDir.FullPath}");

        if (!FileExists($"{coverageDir.FullPath}/coverage.xml"))
        {
            Warning("Coverage file was not generated!");
        }
    })
    .OnError(ex =>
    {
        Error($"Test task failed with error: {ex.Message}");
    });



/// <summary>
/// Task to run end-to-end tests.
/// </summary>
Task("TestE2E")
    .IsDependentOn("Build")
    .Does(() =>
    {
        // Get test projects from Aikido.Zen.Test.End2End directory
        var testProjects = GetFiles("./Aikido.Zen.Test.End2End/*.csproj");
        foreach (var project in testProjects)
        {
            DotNetTest(project.FullPath, new DotNetTestSettings
            {
                DiagnosticOutput = true,
                Configuration = configuration,
                NoBuild = true,
                NoRestore = true,
                ArgumentCustomization = args => args
                    .Append("--verbosity detailed")
                    .Append("--logger console;verbosity=detailed")
            });
        }
        Information($"TestE2E task completed successfully.");
    });

Task("Pack")
    .Does(() =>
    {
        if (configuration == "Release")
        {
            var projects = new[] {
                "./Aikido.Zen.DotNetFramework/Aikido.Zen.DotNetFramework.csproj",
                "./Aikido.Zen.DotNetCore/Aikido.Zen.DotNetCore.csproj"
            };

            foreach (var project in projects)
            {
                var specFile = project.Replace(".csproj", ".nuspec");
                var nugetPackSettings = new NuGetPackSettings
                {
                    OutputDirectory = "./artifacts",
                    Version = libVersion,
                };
                NuGetPack(specFile, nugetPackSettings);
            }
            Information("Pack task completed successfully.");
        }
        else
        {
            Information("Skipping NuGet package creation as configuration is not Release.");
        }
    });

Task("CreatePackages")
    .IsDependentOn("Build")
    .IsDependentOn("Pack");

Task("Default")
    .IsDependentOn("Build");

RunTarget(target);
