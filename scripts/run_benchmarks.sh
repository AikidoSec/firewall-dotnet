#!/bin/bash

# Run the sample app creation script
./create_sample_app.sh

# Define sample apps
SAMPLE_APPS=("MySqlSampleApp" "PostgresSampleApp" "SqlServerSampleApp")

# Run benchmarks for each app
for app in "${SAMPLE_APPS[@]}"; do
    # Run k6 benchmark
    ./scripts/k6.sh summary_no_zen.json
    no_zen_time=$(jq -r 'select(.type=="Point" and .metric=="http_req_duration") | .data.value' summary_no_zen.json | jq -s add/length)

    # Run wrk benchmark
    ./scripts/wrk.sh http://localhost:5081
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