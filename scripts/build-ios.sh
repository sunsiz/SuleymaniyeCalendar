#!/usr/bin/env bash
set -euo pipefail

# Build script for iOS simulator
# Usage: ./scripts/build-ios.sh [RID] [Configuration]
# Example: ./scripts/build-ios.sh iossimulator-x64 Debug

RID=${1:-iossimulator-x64}
CONFIG=${2:-Debug}
PROJECT="SuleymaniyeCalendar/SuleymaniyeCalendar.csproj"
TFM="net9.0-ios"

echo "Building $PROJECT for $TFM / $RID ($CONFIG)"
dotnet build "$PROJECT" -f $TFM -r $RID -c $CONFIG --no-incremental

echo "Build finished. Output under SuleymaniyeCalendar/bin/Debug/net9.0-ios/"
