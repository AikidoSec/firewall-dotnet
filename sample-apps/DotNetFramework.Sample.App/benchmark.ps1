$projectDir = Get-Location

$baseAddress = "http://localhost:5095/"
$helloEndpoint = $baseAddress + "benchmark/hello"

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

# Function to start the application
function Start-Application {
    param (
        [string]$aikidoDisable
    )
    [System.Environment]::SetEnvironmentVariable("AIKIDO_DISABLE", $aikidoDisable, [System.EnvironmentVariableTarget]::Process)
    Start-Process -FilePath "C:\Program Files\IIS Express\iisexpress.exe" -ArgumentList "/path:$projectDir /port:5095" -NoNewWindow -PassThru -RedirectStandardOutput "%temp%\iisexpress.log"
    Start-Sleep -Seconds 5
}

# Function to stop the application
function Stop-Application {
    Stop-Process -Name "iisexpress" -Force
}

# Start the application with firewall enabled
Start-Application -aikidoDisable "false"
Write-Host "application started with firewall enabled"

# Check if the application is available on the given port using curl with a 5 second timeout
$isAvailable = wsl curl -k -s -o /dev/null -w "%{http_code}" --connect-timeout 5 $helloEndpoint

Write-Host "HTTP status code: $isAvailable"

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

# Check if the application is available on the given port using curl with a 5 second timeout
$isAvailable = wsl curl -k -s -o /dev/null -w "%{http_code}" --connect-timeout 5 $helloEndpoint

Write-Host "HTTP status code: $isAvailable"

if ($isAvailable -eq 200) {
    Write-Host "running benchmark without firewall for endpoint $helloEndpoint"
    wsl wrk -t12 -c400 -d30s --latency $helloEndpoint
} else {
    Write-Host "application is not available on the given port, HTTP status code: $isAvailable"
}

# Stop the application
Write-Host "stopping the application"
Stop-Application
