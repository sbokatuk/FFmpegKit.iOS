#!/usr/bin/env bash
set -euo pipefail

# Builds the device test app against a packed FFmpegKit package, installs it on an iOS simulator
# and runs its smoke tests. The app prints its verdict to stdout; this script turns that into an
# exit code.
#
# Usage: run-simulator-tests.sh [VARIANT] VERSION [TARGET_FRAMEWORK]

VARIANT="${1:-Video}"
VERSION="${2:?a package version is required}"
TARGET_FRAMEWORK="${3:-net10.0-ios26.0}"

BUNDLE_ID="com.sbokatuk.ffmpegkit.devicetests"
LOG_FILE="simulator-tests.log"
# CI runners are Apple silicon. Override for an Intel runner, whose simulator is x64.
SIMULATOR_RID="${FFMPEGKIT_SIMULATOR_RID:-iossimulator-arm64}"
SIMULATOR_DEVICE="${FFMPEGKIT_SIMULATOR_DEVICE:-iPhone 17}"

REPO_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
PROJECT="${REPO_ROOT}/tests/FFmpegKit.iOS.DeviceTests/FFmpegKit.iOS.DeviceTests.csproj"

# The .NET 9 band builds net8/net9 and the .NET 10 band builds net9/net10, so pick the SDK that
# owns the requested target framework. The SDK is resolved from the working directory, and the
# repository's global.json pins .NET 9, hence the scratch directory.
case "${TARGET_FRAMEWORK}" in
    net10.0-*) sdk_major=10 ;;
    *)         sdk_major=9 ;;
esac

sdk_version="$(dotnet --list-sdks | grep "^${sdk_major}\." | tail -1 | cut -d' ' -f1)"
if [ -z "${sdk_version}" ]; then
    echo "::error::no .NET ${sdk_major} SDK installed, cannot build ${TARGET_FRAMEWORK}"
    exit 1
fi

SDK_DIR="$(mktemp -d)"
trap 'rm -rf "${SDK_DIR}"' EXIT
printf '{ "sdk": { "version": "%s", "rollForward": "latestFeature" } }\n' "${sdk_version}" \
    > "${SDK_DIR}/global.json"

# NuGet caches by package id + version, so rebuilding a version that was already restored once
# silently reuses the stale copy. CI versions are unique, but locally you will re-pack the same
# version repeatedly and test yesterday's bits without this.
package_id="ffmpegkit.net.$(printf '%s' "${VARIANT}" | tr '[:upper:]' '[:lower:]').ios"
rm -rf "${HOME}/.nuget/packages/${package_id}/${VERSION}"

echo "==> building device tests (variant=${VARIANT}, version=${VERSION}, tfm=${TARGET_FRAMEWORK}, sdk=${sdk_version})"
( cd "${SDK_DIR}" && dotnet build "${PROJECT}" \
    --configuration Release \
    -p:FFmpegKitVariant="${VARIANT}" \
    -p:FFmpegKitPackageVersion="${VERSION}" \
    -p:FFmpegKitDeviceTargetFramework="${TARGET_FRAMEWORK}" \
    -p:RuntimeIdentifier="${SIMULATOR_RID}" )

APP_PATH="$(find "${REPO_ROOT}/tests/FFmpegKit.iOS.DeviceTests/bin/Release/${TARGET_FRAMEWORK}/${SIMULATOR_RID}" \
    -maxdepth 1 -name '*.app' -print -quit)"
if [ -z "${APP_PATH}" ]; then
    echo "::error::no .app bundle was produced"
    exit 1
fi

echo "==> booting simulator (${SIMULATOR_DEVICE})"
# Reuse an existing device of this type if there is one, so repeat local runs stay fast.
udid="$(xcrun simctl list devices available --json \
    | python3 -c "
import json, sys
devices = json.load(sys.stdin)['devices']
for runtime in sorted(devices, reverse=True):
    for device in devices[runtime]:
        if device['name'] == '${SIMULATOR_DEVICE}':
            print(device['udid'])
            raise SystemExit
")"

if [ -z "${udid}" ]; then
    echo "::error::no available simulator named '${SIMULATOR_DEVICE}'"
    xcrun simctl list devices available
    exit 1
fi

xcrun simctl boot "${udid}" 2>/dev/null || true
xcrun simctl bootstatus "${udid}" -b

echo "==> installing"
xcrun simctl install "${udid}" "${APP_PATH}"

echo "==> running"
# --console-pty streams the app's stdout straight back, so the verdict needs no log scraping.
# The app terminates itself once it has printed FFMPEGKIT_E2E_DONE.
set +e
xcrun simctl launch --console-pty "${udid}" "${BUNDLE_ID}" 2>&1 | tee "${LOG_FILE}"
set -e

if ! grep -q "FFMPEGKIT_E2E_DONE PASS" "${LOG_FILE}"; then
    # No verdict usually means the app died before reporting, so keep the crash trace.
    echo "==> no passing verdict; capturing crash output"
    xcrun simctl spawn "${udid}" log show --last 2m --predicate "process == 'FFmpegKit.iOS.DeviceTests'" \
        2>/dev/null | tail -100 | tee -a "${LOG_FILE}" || true
    echo "::error::FFmpegKit simulator smoke tests failed or timed out"
    exit 1
fi

echo "==> simulator smoke tests passed"
