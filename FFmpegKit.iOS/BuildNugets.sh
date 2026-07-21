#!/bin/sh

set -e

# Builds and packs every FFmpegKit variant. Run FetchXcFrameworks.sh first.
#
# Usage:
#   ./BuildNugets.sh                       # version from Directory.Build.props
#   ./BuildNugets.sh 8.1.2-beta.4          # explicit package version
#   ./BuildNugets.sh 7.1.1 7.1.1           # ...and bind an older FFmpegKit line
#
# The second argument selects which native FFmpegKit version to bind. Run
# FetchXcFrameworks.sh with the same version first, so the matching xcframeworks are present.
#
# Packages are written to ../artifacts.
#
# Each .NET SDK's iOS workload supports only two target frameworks - the .NET 9 band builds
# net8/net9, the .NET 10 band builds net9/net10 - so this runs two passes and merges them. The
# repository's global.json pins the .NET 9 SDK, so the second pass is invoked from a scratch
# directory carrying its own global.json, since the SDK is resolved from the working directory.

cd "$(dirname "$0")"

VERSION="$1"
NATIVE_VERSION="$2"
ROOT="$(cd .. && pwd)"
PROJECT="$ROOT/FFmpegKit.iOS/FFmpegKit.iOS.csproj"
OUTPUT="$ROOT/artifacts"

PASS1_BAND="net9"
PASS2_BAND="net10"
PASS2_SDK="10.0.100"

VERSION_ARG=""
if [ -n "$VERSION" ]; then
    # Validated before being interpolated into MSBuild arguments and package file names.
    case "$VERSION" in
        *[!A-Za-z0-9.+_-]*)
            echo "error: invalid version '$VERSION'" >&2
            exit 1
            ;;
    esac
    VERSION_ARG="-p:Version=$VERSION"
fi

NATIVE_ARG=""
if [ -n "$NATIVE_VERSION" ]; then
    case "$NATIVE_VERSION" in
        *[!A-Za-z0-9.+_-]*)
            echo "error: invalid native version '$NATIVE_VERSION'" >&2
            exit 1
            ;;
    esac
    NATIVE_ARG="-p:FFmpegKitNativeVersion=$NATIVE_VERSION"
fi

# NuGet.config declares ./artifacts as a package source, and restore fails outright with NU1301
# if a local source directory is missing - before anything here has had a chance to create it as
# an output directory. A fresh clone only has it because an empty .gitkeep is committed, so make
# the build independent of that surviving.
mkdir -p "$OUTPUT"

PASS1_DIR="$OUTPUT/.net9-pass"
PASS2_DIR="$OUTPUT/.net10-pass"
rm -rf "$PASS1_DIR" "$PASS2_DIR"

SDK10_DIR="$(mktemp -d)"
trap 'rm -rf "$SDK10_DIR"' EXIT
cat > "$SDK10_DIR/global.json" <<EOF
{ "sdk": { "version": "$PASS2_SDK", "rollForward": "latestFeature" } }
EOF

for build_type in Audio Full FullGpl Https HttpsGpl Min MinGpl Video; do
    echo "==> packing $build_type ($PASS1_BAND band)"
    dotnet pack "$PROJECT" \
        -c Release \
        -p:FFmpegKitBuildType="$build_type" \
        -p:FFmpegKitSdkBand="$PASS1_BAND" \
        $VERSION_ARG $NATIVE_ARG \
        -o "$PASS1_DIR"

    echo "==> packing $build_type ($PASS2_BAND band)"
    (cd "$SDK10_DIR" && dotnet pack "$PROJECT" \
        -c Release \
        -p:FFmpegKitBuildType="$build_type" \
        -p:FFmpegKitSdkBand="$PASS2_BAND" \
        $VERSION_ARG $NATIVE_ARG \
        -o "$PASS2_DIR")
done

echo "==> merging target frameworks"
python3 "$ROOT/build/merge-packages.py" "$PASS1_DIR" "$PASS2_DIR" "$OUTPUT"

rm -rf "$PASS1_DIR" "$PASS2_DIR"
