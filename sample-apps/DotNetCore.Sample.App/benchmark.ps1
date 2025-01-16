# Check if WSL is installed
if (-Not (Get-Command wsl -ErrorAction SilentlyContinue)) {
    Write-Host "WSL is not installed. Please install WSL manually."
    exit 1
}

# Define the path to the Bash script
$bashScriptPath = "./benchmark.sh"

# Run the Bash script using WSL
Write-Host "Running the benchmark script using WSL..."
wsl bash $bashScriptPath