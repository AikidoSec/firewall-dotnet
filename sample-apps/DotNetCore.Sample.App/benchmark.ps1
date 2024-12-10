
$projectDir = ".\"

$baseAddress = "http://localhost:5095/"

$withFirewallEndpoint = $baseAddress + "benchmark/with-firewall"
$withoutFirewallEndpoint = $baseAddress + "benchmark/without-firewall"

# Navigate to the directory containing your .NET project
cd $projectDir

# Build the .NET Core application
Start-Process -FilePath "dotnet" -ArgumentList "run --project $projectDir\DotNetCore.Sample.App.csproj --configuration Release" -NoNewWindow -PassThru

# Wait for the application to start
Start-Sleep -Seconds 5

# Check if the application is available on the given port using curl
$isAvailable = wsl curl -s -o /dev/null -w "%{http_code}" $withFirewallEndpoint

if ($isAvailable -eq 200) {
    Write-Host "running benchmark with firewall for endpoint $withFirewallEndpoint"
    wsl wrk -t 12 -c 400 -d 30s --latency $withFirewallEndpoint
    
    Write-Host "running benchmark without firewall for endpoint $withoutFirewallEndpoint"
    wsl wrk -t12 -c400 -d30s --latency $withoutFirewallEndpoint
} else {
    Write-Host "application is not available on the given port " + $isAvailable
}

Write-Host "stopping the application"
Stop-Process -Name "dotnet" -Force