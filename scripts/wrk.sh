#!/bin/bash

# Check if wrk is already installed
if ! command -v wrk &> /dev/null; then
    # Update package list and install wrk
    sudo apt-get update
    sudo apt-get install -y wrk
fi

# Run wrk benchmark
wrk -t12 -c400 -d15s $1 > /dev/null 2>&1