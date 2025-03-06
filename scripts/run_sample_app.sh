#!/bin/bash

# Check if a .csproj file is provided
if [ -z "$1" ]; then
    echo "[✗] No .csproj file provided."
    exit 1
fi

# Build the sample app
echo "[✓] Building the sample app..."
dotnet build "$1" # > /dev/null 2>&1

# Run the sample app
echo "[✓] Running the sample app..."
dotnet run --project "$1" --urls "http://localhost:5081" &
APP_PID=$!
sleep 2

# Check if the app is running
echo "[✓] Checking health endpoint..."
for i in {1..5}; do
    echo "[✓] Attempt $i of 5..."
    # Add timeout to curl to prevent hanging
    response=$(curl -s --max-time 10 -w "%{http_code}" http://localhost:5081/health)
    curl_exit=$?

    echo "[✓] Curl exit code: $curl_exit"

    if [ $curl_exit -eq 28 ]; then
        echo "[✗] Request timed out"
        continue
    fi

    if [ -z "$response" ]; then
        echo "[✗] No response received from health endpoint"
        kill $APP_PID
        exit 1
    fi

    echo "[✓] Response received: $response"

    if [ "$response" -eq 200 ]; then
        echo "[✓] Health endpoint responded with status code $response"
        break
    fi

    if [ $i -eq 5 ]; then
        echo "[✗] Health endpoint failed to respond after 5 attempts"
        kill $APP_PID
        exit 1
    fi
    sleep 4
done
