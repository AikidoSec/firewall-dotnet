#!/usr/bin/env bash

# Get script directory, equivalent to PowerShell's $PSScriptRoot
scriptPath="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

cd "$scriptPath"

# Equivalent to PowerShell's $ErrorActionPreference = 'Stop'
set -e

export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

dotnet tool restore
# Check if the tool restore failed
if [ $? -ne 0 ]; then exit $?; fi

dotnet cake "$@"
# Check if the cake build failed
if [ $? -ne 0 ]; then exit $?; fi
