param (
    [string]$buildConfiguration = "Debug"
)

# Define locations
$outputPath = "../nupkgs"
$project = "../Aikido.Zen.DotNetFramework/Aikido.Zen.DotNetFramework.csproj"
# define sample apps
$sampleApps = @("../sample-apps/sql-injection-framework/packages.config")
# pack without building
Write-Host "Packing Aikido.Zen.DotNetFramework..."
$dateTimeStamp = (Get-Date -Format "yyyyMMddHHmmss")

nuget spec $project
nuget pack $project -Properties Configuration=$buildConfiguration -OutputDirectory $outputPath -Suffix $dateTimeStamp
Write-Host "Packed Aikido.Zen.DotNetFramework"

# Determine the version of the newly packaged framework library
$nupkgFiles = Get-ChildItem -Path $outputPath -Filter "Aikido.Zen.DotNetFramework.*.nupkg"
$latestPackage = $nupkgFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$latestVersion = $latestPackage.Name -replace '^Aikido\.Zen\.DotNetFramework\.|\.nupkg$'

Write-Host "Latest Aikido.Zen.DotNetFramework version: $latestVersion"

# Update the version in the sample apps, uncomment this if you don't want to use project references
# foreach ($app in $sampleApps) {
#     Write-Host "Updating $app with Aikido.Zen.DotNetFramework v$latestVersion"
#     dotnet add $app package 'Aikido.Zen.DotNetFramework' --version $latestVersion
# }
