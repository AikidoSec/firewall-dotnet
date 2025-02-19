# PowerShell script to build all architectures
param(
    [string]$Configuration = "Release",
    [string]$CoreClrPath = $env:CORECLR_PATH,
    [string]$VSVersion = "2022"
)

# Ensure we stop on any error
$ErrorActionPreference = "Stop"

Set-PSDebug -Trace 0
$VerbosePreference = "SilentlyContinue"
$DebugPreference = "SilentlyContinue"

# Helper function to write colored output
function Write-Status {
    param([string]$Message, [string]$Color = "Green")
    Write-Host "==> $Message" -ForegroundColor $Color
}

Write-Status "Script started" -Color Cyan
Write-Status "Current directory: $(Get-Location)" -Color Cyan
Write-Status "Script path: $PSScriptRoot" -Color Cyan
Write-Status "Configuration: $Configuration" -Color Cyan
Write-Status "CoreCLR Path: $CoreClrPath" -Color Cyan
Write-Status "VS Version: $VSVersion" -Color Cyan

$ScriptDir = $PSScriptRoot
$BuildDir = Join-Path $ScriptDir "build"
$RootDir = Split-Path -Parent (Split-Path -Parent $ScriptDir)

# If CoreCLR path is not set, try to find it
if (-not $CoreClrPath) {
    $PossiblePaths = @(
        (Join-Path $ScriptDir "..\coreclr"),
        (Join-Path $ScriptDir "coreclr"),
        "%USERPROFILE%\source\repos\coreclr"
    )

    foreach ($Path in $PossiblePaths) {
        if (Test-Path $Path) {
            $CoreClrPath = $Path
            Write-Status "Found CoreCLR at: $CoreClrPath" -Color Green
            break
        }
        else {
            Write-Status "CoreCLR path not found: $Path" -Color Yellow
        }
    }
}

# Verify CoreCLR path
if (-not $CoreClrPath) {
    Write-Status "CoreCLR path not found. Please clone the runtime repository:" -Color Red
    Write-Status "git clone https://github.com/dotnet/runtime.git" -Color Yellow
    exit 1
}

if (-not (Test-Path $CoreClrPath)) {
    Write-Status "CoreCLR path does not exist: $CoreClrPath" -Color Red
    exit 1
}

# Verify Visual Studio installation
$VSWhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
Write-Status "Looking for Visual Studio at: $VSWhere" -Color Cyan
if (-not (Test-Path $VSWhere)) {
    Write-Status "Visual Studio installer not found. Please install Visual Studio $VSVersion" -Color Red
    exit 1
}

$VSPath = & $VSWhere -version "[17.0,18.0)" -property installationPath
Write-Status "Found Visual Studio at: $VSPath" -Color Cyan
if (-not $VSPath) {
    Write-Status "Visual Studio $VSVersion not found. Please install Visual Studio $VSVersion" -Color Red
    exit 1
}

# Create build directory
Write-Status "Creating build directory..."
New-Item -ItemType Directory -Force -Path $BuildDir | Out-Null

# Verify CMake installation
$CMake = Get-Command "cmake.exe" -ErrorAction SilentlyContinue
if (-not $CMake) {
    Write-Status "CMake not found. Installing via winget..." -Color Yellow
    & winget install Kitware.CMake

    # Refresh PATH
    $env:Path = [System.Environment]::GetEnvironmentVariable("Path", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path", "User")

    $CMake = Get-Command "cmake.exe" -ErrorAction SilentlyContinue
    if (-not $CMake) {
        Write-Status "CMake installation failed. Please install CMake manually from https://cmake.org/download/" -Color Red
        exit 1
    }
}

Write-Status "Found CMake at: $($CMake.Source)" -Color Green
Write-Status "CMake version: $(& cmake --version)" -Color Green

# Run build_all.cmake to create toolchain files
Write-Status "Running build_all.cmake to create toolchain files..." -Color Cyan
Push-Location $ScriptDir
try {
    & cmake -P build_all.cmake
} catch {
    Write-Status "Failed to run build_all.cmake" -Color Red
    exit 1
} finally {
    Pop-Location
}

# Proceed with building for all platforms and architectures

# Define supported platforms and architectures
$platforms = @("windows", "linux", "osx")
$architectures = @("x64", "arm", "arm64")

# Ensure the CMake command uses the correct path for the source directory
$sourceDir = $PSScriptRoot

foreach ($platform in $platforms) {
    foreach ($arch in $architectures) {
        $toolchainFile = Join-Path $PSScriptRoot "cmake/toolchains/toolchain_${platform}_${arch}.cmake"
        if (Test-Path $toolchainFile) {
            $buildDir = Join-Path $PSScriptRoot "build/${platform}/${arch}"
            New-Item -ItemType Directory -Force -Path $buildDir | Out-Null

            # Ensure bin directory exists
            $binDir = Join-Path $PSScriptRoot "bin"
            New-Item -ItemType Directory -Force -Path $binDir | Out-Null

            Push-Location $buildDir
            try {
                Write-Status "Building for $platform $arch..." -Color Cyan
                & cmake -DCMAKE_TOOLCHAIN_FILE="$toolchainFile" `
                       -DCMAKE_BUILD_TYPE=$Configuration `
                       -DCORECLR_PATH=$CoreClrPath `
                       -DSCRIPT_DIR="$sourceDir" `
                       $sourceDir
                & cmake --build . --config $Configuration

                if ($LASTEXITCODE -ne 0) {
                    Write-Status "Build failed for $platform $arch" -Color Red
                    exit 1
                }
            } catch {
                Write-Status "Build failed for $platform $arch" -Color Red
                exit 1
            } finally {
                Pop-Location
            }
        } else {
            Write-Status "Toolchain file not found: $toolchainFile" -Color Yellow
        }
    }
}

Write-Status "Build completed successfully" -Color Green
Write-Status "Output files can be found in: $(Join-Path $PSScriptRoot 'bin')" -Color Green

Write-Status "Script completed" -Color Cyan
