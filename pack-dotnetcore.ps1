param (
    [string]$buildConfiguration = "Debug"
)

# Define locations
$outputPath = "../nupkgs"
$project = "../Aikido.Zen.DotNetCore/Aikido.Zen.DotNetCore.csproj"
# define sample apps
$sampleApps = @("../sample-apps/sql-injection-core/sql-injection-core.csproj")
# pack without building
Write-Host "Packing Aikido.Zen.DotNetCore..."
$dateTimeStamp = (Get-Date -Format "yyyyMMddHHmmss")

dotnet pack $coreProject --configuration $buildConfiguration -o $outputPath --no-build --version-suffix $dateTimeStamp
Write-Host "Packed Aikido.Zen.DotNetCore"

# Determine the version of the newly packaged core library
$nupkgFiles = Get-ChildItem -Path $outputPath -Filter "Aikido.Zen.DotNetCore.*.nupkg"
$latestPackage = $nupkgFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$latestVersion = $latestPackage.Name -replace '^Aikido\.Zen\.DotNetCore\.|\.nupkg$'

Write-Host "Latest Aikido.Zen.Core version: $latestVersion"

# Update the version in the sample apps
foreach ($app in $sampleApps) {
    Write-Host "Updating $app with Aikido.Zen.Core v$latestVersion"
    dotnet add $app package 'Aikido.Zen.Core' --version $latestVersion
}
