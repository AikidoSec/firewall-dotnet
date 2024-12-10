$projectDir = Get-Location

$baseAddress = "http://localhost:5095/"

$withFirewallEndpoint = $baseAddress + "benchmark/withfirewall"
$withoutFirewallEndpoint = $baseAddress + "benchmark/withoutfirewall"

# Navigate to the directory containing your .NET project
cd $projectDir

function getMsbuildPath() {
    $msbuildPath = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\msbuild.exe"
    return $msbuildPath
}

#get msbuildPath
$msbuildPath = getMsbuildPath

$buildPath = Join-Path -Path $projectDir -ChildPath "bin\DotNetFramework.Sample.App.dll"

Write-Host "msbuild path: " + $msbuildPath

# Build the .NET Framework application 
& $msbuildPath "$projectDir\DotNetFramework.Sample.App.csproj" /p:Configuration=Release

# Start the .NET Framework application using IIS Express, but we don't want its output
Start-Process -FilePath "C:\Program Files\IIS Express\iisexpress.exe" -ArgumentList "/path:$projectDir /port:5095" -NoNewWindow -PassThru -RedirectStandardOutput "%temp%\iisexpress.log"

# Wait for the application to start
Start-Sleep -Seconds 5

Write-Host "application started"

# Check if the application is available on the given port using curl with a 5 second timeout
$isAvailable = wsl curl -k -s -o /dev/null -w "%{http_code}" --connect-timeout 5 $withFirewallEndpoint

Write-Host "HTTP status code: $isAvailable"

if ($isAvailable -eq 200) {
    Write-Host "running benchmark without firewall for endpoint $withoutFirewallEndpoint"
    wsl wrk -t12 -c400 -d30s --latency $withoutFirewallEndpoint
    
    Write-Host "running benchmark with firewall for endpoint $withFirewallEndpoint"
    wsl wrk -t 12 -c 400 -d 30s --latency $withFirewallEndpoint
    
} else {
    Write-Host "application is not available on the given port, HTTP status code: $isAvailable"
}

Write-Host "stopping the application"
Stop-Process -Name "iisexpress" -Force
