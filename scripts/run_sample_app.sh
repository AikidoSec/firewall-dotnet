#!/bin/bash

# Check if a .csproj file is provided
if [ -z "$1" ]; then
    echo "[✗] No .csproj file provided."
    exit 1
fi

# Build the sample app
echo "[✓] Building the sample app..."
dotnet build "$1" -f net8.0 # > /dev/null 2>&1

# Run the sample app
echo "[✓] Running the sample app..."
dotnet run --project "$1" --urls "http://localhost:5081" # > /dev/null 2>&1 &
sleep 2

# Check if the app is running
echo "[✓] Checking health endpoint..."
for i in {1..5}; do
    response=$(curl -s -w "%{http_code}" http://localhost:5081/health || true)
    if [ -z "$response" ]; then
        echo "[✗] No response received from health endpoint"
        exit 1
    fi
    if [ "$response" -eq 200 ]; then
        break
    fi
    if [ $i -eq 5 ]; then
        echo "[✗] Health endpoint failed to respond after 5 attempts"
        exit 1
    fi
    sleep 2
done
