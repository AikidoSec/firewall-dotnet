#!/bin/bash

# Check if k6 is already installed
if ! command -v k6 &> /dev/null; then
    # Add k6 key and repository
    sudo apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys C5AD17C747E3415A3642D57D77C6C491D6AC1D69
    echo "deb https://dl.k6.io/deb stable main" | sudo tee /etc/apt/sources.list.d/k6.list
    # Update package list and install k6
    sudo apt-get update
    sudo apt-get install -y k6
fi

# Run k6 benchmark
k6 run --quiet --out json=$1 benchmark.js > /dev/null 2>&1