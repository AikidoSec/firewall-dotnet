#!/bin/bash

# Check if a .csproj file is provided
if [ -z "$1" ]; then
    echo "[✗] No .csproj file provided."
    exit 1
fi

# Build the sample app first
echo "...Building the sample app..."
dotnet build "$1" > /dev/null 2>&1

# Check if mock server is running and ready
echo "...Checking mock server health..."
for i in {1..5}; do
    echo "... Checking mock server - attempt $i of 5..."
    response=$(curl -s --max-time 10 -w "%{http_code}" http://localhost:5080/health)
    curl_exit=$?

    if [ $curl_exit -eq 0 ] && [ "$response" -eq 200 ]; then
        echo "[✓] Mock server is ready"
        break
    elif [ "$response" -eq 000 ]; then
        echo "[✗] Mock server responded with status code 000. Attempting to start the mock server again..."
        dotnet run --project e2e/Aikido.Zen.Server.Mock/Aikido.Zen.Server.Mock.csproj --urls "http://localhost:5080" &
        sleep 5  # Give the mock server some time to start
    else
        echo "[✗] Mock server responded with status code $response"
    fi


    if [ $i -eq 5 ]; then
        echo "[✗] Mock server failed to respond after 5 attempts"
        exit 1
    fi
    sleep 4
done

# Run the sample app
echo "...Running the sample app..."
dotnet run --project "$1" --urls "http://localhost:5081" &
APP_PID=$!
sleep 5  # Increased sleep time to give the app more time to start

# Check if the app is running
echo "...Checking health endpoint..."
for i in {1..5}; do
    echo "[✓] Attempt $i of 5..."
    response=$(curl -s --max-time 10 -o /dev/null -w "%{http_code}" http://localhost:5081/health)

    if [ $curl_exit -eq 28 ]; then
        echo "[✗] Request timed out"
        continue
    fi

    if [ -z "$response" ]; then
        echo "[✗] No response received from health endpoint"
        kill $APP_PID
        exit 1
    fi

    # Echo only the status code, which is stored in $response
    echo "[✓] Response received: $response"

    if [ "$response" -eq 200 ]; then
        echo "[✓] Health endpoint responded with status code $response"
        # Export the PID so other scripts can use it
        echo $APP_PID > .app.pid
        exit 0
    fi

    if [ $i -eq 5 ]; then
        echo "[✗] Health endpoint failed to respond after 5 attempts"
        kill $APP_PID
        exit 1
    fi
    sleep 4
done
