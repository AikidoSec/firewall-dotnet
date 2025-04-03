#!/bin/bash

# Check if jq is installed
if ! command -v jq &> /dev/null; then
    # Install jq
    sudo apt-get install -y jq
fi

# Define sample apps
SAMPLE_APPS=("MySqlSampleApp" "PostgresSampleApp" "SqlServerSampleApp")

# Function to stop the running app
stop_app() {
    if [ -f .app.pid ]; then
        APP_PID=$(cat .app.pid)
        kill $APP_PID 2>/dev/null || true
        rm .app.pid
    fi
    pkill dotnet || true
    sleep 2
}

# Run benchmarks for each app
for app in "${SAMPLE_APPS[@]}"; do
    # Run the sample app with AIKIDO_DISABLED=true
    export AIKIDO_DISABLED=true
    ./scripts/run_sample_app.sh e2e/sample-apps/$app/$app.csproj

    # Run k6 benchmark
    ./scripts/k6.sh summary_no_zen.json
    if [ ! -f "summary_no_zen.json" ]; then
        echo "[✗] k6 benchmark failed to create summary_no_zen.json."
        stop_app
        exit 1
    fi
    no_zen_time=$(jq -r 'select(.type=="Point" and .metric=="http_req_duration") | .data.value' summary_no_zen.json | jq -s add/length)

    # Run wrk benchmark
    wrk_no_zen_output=$(./scripts/wrk.sh http://localhost:5081)
    if [ -z "$wrk_no_zen_output" ]; then
        echo "[✗] wrk benchmark failed to produce output."
        stop_app
        exit 1
    fi
    no_zen_throughput=$(echo "$wrk_no_zen_output" | grep "Requests/sec" | awk '{print $2}')

    # Stop the app
    stop_app

    # Run the sample app with AIKIDO_DISABLED=false
    export AIKIDO_DISABLED=false
    ./scripts/run_sample_app.sh e2e/sample-apps/$app/$app.csproj

    # Run k6 benchmark
    ./scripts/k6.sh summary_zen.json
    if [ ! -f "summary_zen.json" ]; then
        echo "[✗] k6 benchmark failed to create summary_zen.json."
        stop_app
        exit 1
    fi
    zen_time=$(jq -r 'select(.type=="Point" and .metric=="http_req_duration") | .data.value' summary_zen.json | jq -s add/length)

    # Run wrk benchmark
    wrk_zen_output=$(./scripts/wrk.sh http://localhost:5081)
    if [ -z "$wrk_zen_output" ]; then
        echo "[✗] wrk benchmark failed to produce output."
        stop_app
        exit 1
    fi
    zen_throughput=$(echo "$wrk_zen_output" | grep "Requests/sec" | awk '{print $2}')

    # Calculate and log throughput difference
    throughput_diff=$(echo "scale=2; (($zen_throughput - $no_zen_throughput) / $no_zen_throughput) * 100" | bc)
    if (( $(echo "$throughput_diff < 5" | bc -l) )); then
        echo "[✓] Throughput difference for $app: $throughput_diff%"
    else
        echo "[✗] Throughput difference for $app: $throughput_diff%"
    fi

    # Compare response times
    diff=$(echo "$zen_time - $no_zen_time" | bc)
    if (( $(echo "$diff > 5" | bc -l) )); then
      echo "[✗] Performance degradation is more than 5ms for $app: $diff ms"
      stop_app
      exit 1
    else
      echo "[✓] Performance within threshold for $app: $diff ms"
    fi

    # Stop the app
    stop_app
done
