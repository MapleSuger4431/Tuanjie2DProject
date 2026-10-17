#!/bin/bash
# Build script for RoslynValidator on Unix/Linux/macOS
# Usage: ./build.sh [runtime]

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUTPUT_DIR="${SCRIPT_DIR}/bin"
PROJECT="${SCRIPT_DIR}/RoslynValidator.csproj"
if [[ -n "${1:-}" ]]; then
    RUNTIME="$1"
else
    case "$(uname -s)" in
        Darwin) OS="osx" ;;
        Linux) OS="linux" ;;
        *) echo "Unsupported operating system: $(uname -s). Pass an explicit RID." >&2; exit 1 ;;
    esac
    case "$(uname -m)" in
        x86_64|amd64)
            # uname reports x86_64 when this shell is translated by Rosetta.
            if [[ "${OS}" == "osx" ]] && [[ "$(sysctl -n hw.optional.arm64 2>/dev/null || true)" == "1" ]]; then
                ARCH="arm64"
            else
                ARCH="x64"
            fi
            ;;
        arm64|aarch64) ARCH="arm64" ;;
        *) echo "Unsupported architecture: $(uname -m). Pass an explicit RID." >&2; exit 1 ;;
    esac
    RUNTIME="${OS}-${ARCH}"
fi

echo "Building RoslynValidator for ${RUNTIME}..."

# Clean previous build
rm -rf "${OUTPUT_DIR}"

# Restore dependencies
dotnet restore "${PROJECT}"

# Build and publish
dotnet publish "${PROJECT}" -c Release -r "${RUNTIME}" --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=false \
    -p:PublishReadyToRun=false \
    -o "${OUTPUT_DIR}"

# dotnet publish also leaves intermediate outputs below bin/Release. They must not
# enter extension archives beside the authoritative self-contained executable.
rm -rf "${OUTPUT_DIR}/Release"

echo ""
echo "Build successful!"
echo "Output: ${OUTPUT_DIR}/RoslynValidator"
echo ""

# Make executable
chmod +x "${OUTPUT_DIR}/RoslynValidator"

# Show usage example
echo "Usage example:"
echo "  ${OUTPUT_DIR}/RoslynValidator --config config.json --json"