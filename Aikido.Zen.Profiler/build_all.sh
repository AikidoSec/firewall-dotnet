#!/bin/bash

# Shell script to build all architectures
CONFIGURATION=${1:-Release}
CORECLR_PATH=${CORECLR_PATH:-""}

# Verify CoreCLR path
if [ -z "$CORECLR_PATH" ]; then
    echo "Error: CORECLR_PATH environment variable must be set"
    exit 1
fi

# Create build directory
BUILD_DIR="$(dirname "$0")/build"
mkdir -p "$BUILD_DIR"

# Function to check required compilers
check_compilers() {
    local missing_compilers=()

    # Check x86/x64 compiler
    if ! command -v gcc &> /dev/null; then
        missing_compilers+=("gcc")
    fi

    # Check ARM compilers
    if ! command -v aarch64-linux-gnu-gcc &> /dev/null; then
        missing_compilers+=("gcc-aarch64-linux-gnu")
    fi

    if ! command -v arm-linux-gnueabihf-gcc &> /dev/null; then
        missing_compilers+=("gcc-arm-linux-gnueabihf")
    fi

    if [ ${#missing_compilers[@]} -ne 0 ]; then
        echo "Error: Missing required compilers: ${missing_compilers[*]}"
        echo "Please install them using your package manager:"
        if command -v apt-get &> /dev/null; then
            echo "sudo apt-get install ${missing_compilers[*]}"
        elif command -v yum &> /dev/null; then
            echo "sudo yum install ${missing_compilers[*]}"
        fi
        exit 1
    fi
}

# Check for required compilers
check_compilers

# Configure CMake
echo "Configuring CMake super-build..."
cd "$BUILD_DIR" || exit 1

cmake -DCORECLR_PATH="$CORECLR_PATH" \
      -P ../build_all.cmake

if [ $? -ne 0 ]; then
    echo "Error: CMake configuration failed"
    exit 1
fi

# Build all architectures
echo "Building all architectures..."
cmake --build . --config "$CONFIGURATION" --target build_all

if [ $? -ne 0 ]; then
    echo "Error: Build failed"
    exit 1
fi

echo "Build completed successfully!"
echo "Output files can be found in $BUILD_DIR/dist"
