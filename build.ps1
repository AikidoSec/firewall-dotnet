# Check if PSScriptRoot is empty and use current directory as fallback
$scriptPath = if ($PSScriptRoot) { 
    $PSScriptRoot 
} else { 
    $PWD.Path 
}

Set-Location -LiteralPath $scriptPath

$ErrorActionPreference = 'Stop'

$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

dotnet tool restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet cake $args
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }