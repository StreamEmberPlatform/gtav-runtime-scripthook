#!/bin/sh
# Builds the ms_abi test targets and runs the detour / stub tests (Linux or WSL, x64).
set -e
cd "$(dirname "$0")"
gcc -O1 -shared -fPIC -o libharness.so harness.c targets.S
HARNESS="$(pwd)/libharness.so" dotnet run -c Release
