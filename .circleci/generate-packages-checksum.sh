#!/bin/bash
set -euo pipefail
IFS=$'\n\t'

# Extract PackageReference lines from all csproj files
find . -type f -name "*.csproj" -exec grep -h "<PackageReference" {} \; | sort > packages.txt

# Also include packages.config files
find . -type f -name "packages.config" -exec cat {} \; | sort >> packages.txt

# Generate checksum
sha256sum packages.txt > packages.checksum 