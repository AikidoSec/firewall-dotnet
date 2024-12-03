param (
    [string]$buildConfiguration = "Debug"
)
# Define locations
$outputPath = "../nupkgs"
$coreProject = "../Aikido.Zen.Core/Aikido.Zen.Core.csproj"
$dotNetFrameworkProject = "../Aikido.Zen.DotNetFramework/packages.config"
$dotNetCoreProject = "../Aikido.Zen.DotNetCore/Aikido.Zen.DotNetCore.csproj"
$frameworkSampleApps = @("../sample-apps/sql-injection-framework/packages.config")

# pack core without building
Write-Host "Packing Aikido.Zen.Core..."
$dateTimeStamp = (Get-Date -Format "yyyyMMddHHmmss")

dotnet pack $coreProject --configuration $buildConfiguration -o $outputPath --no-build --version-suffix $dateTimeStamp
Write-Host "Packed Aikido.Zen.Core"

# Determine the version of the newly packaged core library
$nupkgFiles = Get-ChildItem -Path $outputPath -Filter "Aikido.Zen.Core.*.nupkg"
$latestPackage = $nupkgFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$latestVersion = $latestPackage.Name -replace '^Aikido\.Zen\.Core\.|\.nupkg$'

Write-Host "Latest Aikido.Zen.Core version: $latestVersion"

# Update the version in the dependent projects
Write-Host "Updating $dotNetCoreProject with Aikido.Zen.Core v$latestVersion"
dotnet add $dotNetCoreProject package 'Aikido.Zen.Core' --version $latestVersion
Write-Host "Updating $dotNetFrameworkProject with Aikido.Zen.Core v$latestVersion"
dotnet add $dotNetFrameworkProject package 'Aikido.Zen.Core' --version $latestVersion


# update the version in the sample apps
foreach ($app in $frameworkSampleApps) {
    Write-Host "Updating $app with Aikido.Zen.Core v$latestVersion"
    dotnet add $app package 'Aikido.Zen.Core' --version $latestVersion
}
