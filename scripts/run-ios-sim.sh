#!/usr/bin/env bash
set -euo pipefail

# Build, install and launch the app on an iOS simulator
# Usage: ./scripts/run-ios-sim.sh [SimulatorName]
# Example: ./scripts/run-ios-sim.sh "iPhone 17"

SIM_NAME=${1:-"iPhone 17"}
PROJECT_DIR="SuleymaniyeCalendar"
APP_BUNDLE_SEARCH_DIR="$PROJECT_DIR/bin/Debug/net9.0-ios"
BUNDLE_ID="org.suleymaniyevakfi"

echo "Booting simulator: $SIM_NAME"
UDID=$(xcrun simctl list devices available | grep "$SIM_NAME" | head -n1 | sed -E 's/.*\(([0-9A-F-]+)\).*/\1/')
if [ -z "$UDID" ]; then
  echo "Simulator '$SIM_NAME' not found. Listing available devices:"
  xcrun simctl list devices
  exit 1
fi

echo "Using device UDID: $UDID"

echo "Building app for simulator..."
./scripts/build-ios.sh

echo "Searching for built .app bundle under $APP_BUNDLE_SEARCH_DIR"
APP_PATH=$(find "$APP_BUNDLE_SEARCH_DIR" -name "*.app" -print | head -n1 || true)
if [ -z "$APP_PATH" ]; then
  echo "Built .app not found. Did the build succeed?"
  exit 1
fi

echo "Booting simulator (if not already)"
xcrun simctl boot "$UDID" || true

echo "Installing $APP_PATH on simulator"
xcrun simctl install "$UDID" "$APP_PATH"

echo "Launching $BUNDLE_ID"
xcrun simctl launch "$UDID" "$BUNDLE_ID"

echo "Done. If the app did not show, open the Simulator app and check the device console for errors."
