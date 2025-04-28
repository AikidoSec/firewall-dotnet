#addin nuget:?package=Cake.FileHelpers&version=6.0.0
#load "nuget:https://www.nuget.org/api/v2?package=Cake.NuGet&version=5.0.0"



var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var framework = Argument("framework", "");
var sdkVersion = "";
if (!string.IsNullOrEmpty(framework))
{
    var parts = framework.Split('.');
    if (parts.Length >= 2)
    {
        sdkVersion = $"{parts[0]}.{parts[1]}";
    }
}
var solution = "./Aikido.Zen.sln";
var projectName = "Aikido.Zen.Core";
var zenInternalsVersion = "0.1.37";
var libVersion = Argument("libVersion", "1.2.5");

var baseUrl = $"https://github.com/AikidoSec/zen-internals/releases/download/v{zenInternalsVersion}/";
var librariesDir = $"./{projectName}/libraries";

var filesToDownload = new string[] {
    "libzen_internals_aarch64-apple-darwin.dylib",
    "libzen_internals_aarch64-apple-darwin.dylib.sha256sum",
    "libzen_internals_aarch64-unknown-linux-gnu.so",
    "libzen_internals_aarch64-unknown-linux-gnu.so.sha256sum",
    "libzen_internals_aarch64-pc-windows-msvc.dll",
    "libzen_internals_aarch64-pc-windows-msvc.dll.sha256sum",
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
            // if the version is a prerelease, we need to remove the suffix to avoid assemblyinfo errors
            var version = libVersion.Split('-')[0];
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
            .WithProperty("version", version);

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

/// <summary>
/// Runs unit tests, collects coverage, and ensures errors are propagated.
/// </summary>
Task("Test")
    .IsDependentOn("Build")
    .Does(() =>
    {
        var coverageDir = MakeAbsolute(Directory("./coverage"));
        EnsureDirectoryExists(coverageDir);

        // Get test projects excluding End2End tests
        var testProjects = GetFiles("./**/Aikido.Zen.Test*.csproj")
            .Where(p => !p.FullPath.Contains("End2End"));

        foreach (var project in testProjects)
        {
            // Skip tests for the wrong framework target
            Information($"Checking framework compatibility for {project.FullPath} with target framework {framework}");
            if (framework.StartsWith("4.") && project.FullPath.Contains("DotNetCore"))
            {
                Information($"Skipping test project {project.FullPath} for .NET Framework {framework}");
                continue;
            }
            if (!framework.StartsWith("4.") && !string.IsNullOrEmpty(framework) && project.FullPath.Contains("DotNetFramework"))
            {
                Information($"Skipping test project {project.FullPath} for .NET Core/5+ target {framework}");
                continue;
            }

            var logFileNameBase = project.GetFilenameWithoutExtension();
            var logFilePath = $"{coverageDir.FullPath}/test-results-{logFileNameBase}.trx";
            var outputLogPath = $"{coverageDir.FullPath}/test-output-{logFileNameBase}.log";
            var errorLogPath = $"{coverageDir.FullPath}/test-error-{logFileNameBase}.log";

            // Create or clear the log files
            FileWriteText(outputLogPath, string.Empty);
            FileWriteText(errorLogPath, string.Empty);

            var stdOutput = new List<string>();
            var stdError = new List<string>();

            var arguments = new ProcessArgumentBuilder()
                .Append("test")
                .AppendQuoted(project.FullPath)
                .Append("--configuration").Append(configuration)
                .Append("--no-build")
                .Append("--no-restore")
                .Append("--verbosity").Append("detailed")
                .Append($"--logger \"trx;LogFileName={logFilePath}\"");

            // Add coverage arguments only for the main tests project
            if (project.FullPath.EndsWith("Aikido.Zen.Tests.csproj"))
            {
                arguments = arguments
                    .Append("/p:CollectCoverage=true")
                    .Append("/p:CoverletOutputFormat=opencover")
                    .Append($"/p:CoverletOutput=\"{coverageDir.FullPath}/coverage.xml\"")
                    .Append("/p:Include=\"[Aikido.Zen.*]*\"")
                    .Append("/p:Exclude=\"[Aikido.Zen.Test*]*\"");
            }

            // If SDK version was extracted from framework argument, use it
            if (!string.IsNullOrEmpty(sdkVersion))
            {
                arguments = arguments.Append($"--framework net{sdkVersion}");
            }

            Information($"Running tests for {project.GetFilename()}...");
            Information($"Command: dotnet {arguments.Render()}");

            try
            {
                var process = StartAndReturnProcess(
                    "dotnet",
                    new ProcessSettings
                    {
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        Silent = true // Prevents Cake from logging stdout/stderr itself
                    }
                );

                // Wait for the process to complete before getting output
                process.WaitForExit();

                // Capture standard output after process exit
                process.GetStandardOutput().ToList().ForEach(line =>
                {
                    stdOutput.Add(line);
                    FileAppendText(outputLogPath, line + Environment.NewLine);
                });

                // Capture standard error after process exit
                process.GetStandardError().ToList().ForEach(line =>
                {
                    stdError.Add(line);
                    FileAppendText(errorLogPath, line + Environment.NewLine);
                });

                if (process.GetExitCode() != 0)
                {
                    // Throwing here will be caught by the task's OnError handler
                    throw new Exception($"Test execution failed for {project.GetFilename()}. Exit code: {process.GetExitCode()}");
                }
                Information($"Tests succeeded for {project.GetFilename()}.");
            }
            catch (Exception ex)
            {
                // Log details before re-throwing
                Error($"Test failed for {project.GetFilename()}: {ex.Message}");
                Information("=== Test Standard Output Log Summary (Last 20 lines) ===");
                stdOutput.TakeLast(20).ToList().ForEach(line => Information(line)); // Log summary

                Information("=== Test Standard Error Log Summary (Last 20 lines) ===");
                stdError.TakeLast(20).ToList().ForEach(line => Error(line)); // Log summary

                Information($"Full test output log: {outputLogPath}");
                Information($"Full test error log: {errorLogPath}");

                // Re-throw the exception to ensure the task fails
                throw;
            }
        }
        Information($"Test task completed. Check logs in: {coverageDir.FullPath}");

        if (!FileExists($"{coverageDir.FullPath}/coverage.xml"))
        {
            Warning("Coverage file 'coverage.xml' was not generated! Check test execution and coverage settings.");
        }
    })
    .OnError(ex =>
    {
        // Log the error and then re-throw to ensure the Cake script fails
        Error($"Test task failed overall: {ex.Message}");
        throw ex; // <-- This ensures the script exits with an error code
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
            // Using StartAndReturnProcess for consistency and error handling
            var arguments = new ProcessArgumentBuilder()
                .Append("test")
                .AppendQuoted(project.FullPath)
                .Append("--configuration").Append(configuration)
                .Append("--no-build")
                .Append("--no-restore")
                .Append("--verbosity").Append("detailed")
                .Append("--logger \"console;verbosity=detailed\""); // Log directly to console

             if (!string.IsNullOrEmpty(sdkVersion))
            {
                arguments = arguments.Append($"--framework net{sdkVersion}");
            }

            Information($"Running E2E tests for {project.GetFilename()}...");
            Information($"Command: dotnet {arguments.Render()}");

            var exitCode = StartProcess("dotnet", new ProcessSettings { Arguments = arguments });

            if (exitCode != 0)
            {
                throw new Exception($"E2E Test execution failed for {project.GetFilename()}. Exit code: {exitCode}");
            }
             Information($"E2E tests succeeded for {project.GetFilename()}.");
        }
        Information($"TestE2E task completed successfully.");
    })
     .OnError(ex =>
    {
        Error($"TestE2E task failed: {ex.Message}");
        throw ex; // Ensure script fails on E2E test error
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

            var outputDir = MakeAbsolute(Directory("./artifacts"));
             EnsureDirectoryExists(outputDir);

            foreach (var projectPath in projects)
            {
                var project = ParseProject(projectPath);
                var nuspecPath = projectPath.Replace(".csproj", ".nuspec");

                 if (!FileExists(nuspecPath)) {
                     Warning($"Nuspec file not found for project {projectPath}, attempting to pack project directly.");
                     // Pack project directly if nuspec is missing
                     DotNetPack(projectPath, new DotNetPackSettings {
                        Configuration = configuration,
                        OutputDirectory = outputDir,
                        ArgumentCustomization = args => args.Append($"/p:PackageVersion={libVersion}")
                     });
                 } else {
                     // Pack using nuspec file
                     NuGetPack(nuspecPath, new NuGetPackSettings
                     {
                        OutputDirectory = outputDir,
                        Version = libVersion,
                        Configuration = configuration, // Pass configuration to potentially use properties
                        Properties = new Dictionary<string, string> { {"Configuration", configuration} } // Ensure configuration is available in nuspec
                     });
                 }
            }
            Information($"Pack task completed successfully. Packages in: {outputDir}");
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
