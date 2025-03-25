#!/bin/bash

# Function to check if a command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Check if curl is installed, if not, install it
if ! command_exists curl; then
    echo "curl is not installed. Installing curl..."
    sudo apt-get update
    sudo apt-get install -y curl
fi

# check if wrk is installed, if not, install it
if ! command_exists wrk; then
    echo "wrk is not installed. Installing wrk..."
    sudo apt-get update
    sudo apt-get install -y wrk
fi

# check if dotnet 9 is installed, if not, install it
if ! command_exists dotnet; then
    echo "dotnet 9 is not installed. Installing dotnet 9..."
    sudo apt-get update
    sudo apt-get install -y dotnet-sdk-9.0
fi

# Define variables
PROJECT_DIR="./"
BASE_ADDRESS="http://localhost:5095/"
HELLO_ENDPOINT="${BASE_ADDRESS}benchmark/hello"

# Function to start the application
start_application() {
    local aikido_disable=$1
    export AIKIDO_DISABLE=$aikido_disable
    export AIKIDO_TOKEN=12456789
    dotnet run --project "${PROJECT_DIR}DotNetCore.Sample.App.csproj" --configuration Release > app.log 2>&1 &
    sleep 10  # Wait for the application to start
}

# Function to stop the application
stop_application() {
    pkill dotnet
}

# Start the application with firewall enabled
start_application "false"
echo "Application started with firewall enabled"

# Check if the application is available
is_available=$(curl -s -o /dev/null -w "%{http_code}" $HELLO_ENDPOINT)

if [ "$is_available" -eq 200 ]; then
    echo "Running benchmark with firewall for endpoint $HELLO_ENDPOINT"
    wrk -t12 -c400 -d30s --latency $HELLO_ENDPOINT
else
    echo "Application is not available on the given port, HTTP status code: $is_available"
    echo "Check app.log for more details."
fi

# Stop the application
echo "Stopping the application"
stop_application

# Start the application with firewall disabled
start_application "true"
echo "Application started with firewall disabled"

# Check if the application is available
is_available=$(curl -s -o /dev/null -w "%{http_code}" $HELLO_ENDPOINT)

if [ "$is_available" -eq 200 ]; then
    echo "Running benchmark without firewall for endpoint $HELLO_ENDPOINT"
    wrk -t12 -c400 -d30s --latency $HELLO_ENDPOINT
else
    echo "Application is not available on the given port, HTTP status code: $is_available"
    echo "Check app.log for more details."
fi

# Stop the application
echo "Stopping the application"
stop_application
