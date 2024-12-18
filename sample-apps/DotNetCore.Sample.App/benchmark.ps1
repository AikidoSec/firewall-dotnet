
$projectDir = ".\"

$baseAddress = "http://localhost:5095/"
$helloEndpoint = $baseAddress + "benchmark/hello"

# Navigate to the directory containing your .NET project
cd $projectDir

function Start-Application {
    param (
        [string]$aikidoDisable
    )
    [System.Environment]::SetEnvironmentVariable("AIKIDO_DISABLE", $aikidoDisable, [System.EnvironmentVariableTarget]::Process)
    Start-Process -FilePath "dotnet" -ArgumentList "run --project $projectDir\DotNetCore.Sample.App.csproj --configuration Release" -NoNewWindow -PassThru
    Start-Sleep -Seconds 5
}

function Stop-Application {
    Stop-Process -Name "dotnet" -Force
}

# Start the application with firewall enabled
Start-Application -aikidoDisable "false"
Write-Host "application started with firewall enabled"

# Check if the application is available on the given port using curl
$isAvailable = wsl curl -s -o /dev/null -w "%{http_code}" $helloEndpoint

if ($isAvailable -eq 200) {
    Write-Host "running benchmark with firewall for endpoint $helloEndpoint"
    wsl wrk -t12 -c400 -d30s --latency $helloEndpoint
} else {
    Write-Host "application is not available on the given port, HTTP status code: $isAvailable"
}

# Stop the application
Write-Host "stopping the application"
Stop-Application

# Start the application with firewall disabled
Start-Application -aikidoDisable "true"
Write-Host "application started with firewall disabled"

# Check if the application is available on the given port using curl
$isAvailable = wsl curl -s -o /dev/null -w "%{http_code}" $helloEndpoint

if ($isAvailable -eq 200) {
    Write-Host "running benchmark without firewall for endpoint $helloEndpoint"
    wsl wrk -t12 -c400 -d30s --latency $helloEndpoint
} else {
    Write-Host "application is not available on the given port, HTTP status code: $isAvailable"
}

# Stop the application
Write-Host "stopping the application"
Stop-Application