#!/bin/bash

# Check if a .csproj file is provided
if [ -z "$1" ]; then
    echo "[✗] No .csproj file provided."
    exit 1
fi

# Run the sample app
dotnet run --project "$1" --urls "http://localhost:5081" > /dev/null 2>&1 &
sleep 2

# Check if the app is running
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