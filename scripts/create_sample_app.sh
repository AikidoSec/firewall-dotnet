#!/bin/bash

# Define sample apps
SAMPLE_APPS=("MySqlSampleApp" "PostgresSampleApp" "SqlServerSampleApp")

# Build and run each sample app
for app in "${SAMPLE_APPS[@]}"; do
    dotnet build e2e/sample-apps/$app/$app.csproj > /dev/null 2>&1
    pkill dotnet || true
    export AIKIDO_DISABLED=true
    dotnet run --project e2e/sample-apps/$app/$app.csproj --urls "http://localhost:5081" > /dev/null 2>&1 &
    sleep 2
    for i in {1..5}; do
        response=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5081/health || true)
        if [ -z "$response" ]; then
            exit 1
        fi
        if [ "$response" -eq 200 ]; then
            break
        fi
        if [ $i -eq 5 ]; then
            echo "Health endpoint failed to respond after 5 attempts"
            exit 1
        fi
        sleep 2
    done
done 