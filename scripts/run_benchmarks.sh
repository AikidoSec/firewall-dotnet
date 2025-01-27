#!/bin/bash

# Check if create_sample_app.sh exists and is executable
if [ ! -x "./scripts/create_sample_app.sh" ]; then
    echo "[✗] create_sample_app.sh is missing or not executable."
    exit 1
fi

# Run the sample app creation script
./scripts/create_sample_app.sh

# Verify if summary_no_zen.json is created
if [ ! -f "summary_no_zen.json" ]; then
    echo "[✗] summary_no_zen.json was not created."
    exit 1
fi

# Define sample apps
SAMPLE_APPS=("MySqlSampleApp" "PostgresSampleApp" "SqlServerSampleApp")

# Run benchmarks for each app
for app in "${SAMPLE_APPS[@]}"; do
    # Run k6 benchmark
    ./scripts/k6.sh summary_no_zen.json
    if [ ! -f "summary_no_zen.json" ]; then
        echo "[✗] k6 benchmark failed to create summary_no_zen.json."
        exit 1
    fi
    no_zen_time=$(jq -r 'select(.type=="Point" and .metric=="http_req_duration") | .data.value' summary_no_zen.json | jq -s add/length)

    # Run wrk benchmark
    ./scripts/wrk.sh http://localhost:5081
    if [ -z "$wrk_no_zen_output" ]; then
        echo "[✗] wrk benchmark failed to produce output."
        exit 1
    fi
    no_zen_throughput=$(echo "$wrk_no_zen_output" | grep "Requests/sec" | awk '{print $2}')

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
      exit 1
    else
      echo "[✓] Performance within threshold for $app: $diff ms"
    fi
done 