param (
    [string]$buildConfiguration = "Debug"
)

# Define locations
$outputPath = "../nupkgs"
$coreProject = "../Aikido.Zen.Core/Aikido.Zen.Core.csproj"
$dotNetFrameworkProject = "../Aikido.Zen.DotNetFramework/packages.config"
$dotNetCoreProject = "../Aikido.Zen.DotNetCore/Aikido.Zen.DotNetCore.csproj"

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
(Get-Content $dotNetCoreProject) -replace '(?i)(?<=<PackageReference Include="Aikido.Zen.Core" version=")[^"]+(?=")', $latestVersion | Set-Content $dotNetCoreProject
(Get-Content $dotNetFrameworkProject) -replace '(?i)(?<=<package id="Aikido.Zen.Core" version=")[^"]+(?=")', $latestVersion | Set-Content $dotNetFrameworkProject


# Build and pack the dependent projects
foreach ($project in $projects) {
    dotnet restore $project
    dotnet build $project --configuration $buildConfiguration
    dotnet pack $project --configuration $buildConfiguration -o $outputPath
}