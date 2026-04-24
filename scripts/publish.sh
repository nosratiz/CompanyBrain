#!/usr/bin/env bash
# -----------------------------------------------------------------------------
#  publish.sh — Native AOT publish for DeepRoot.Photino
#  Produces self-contained, trimmed, single-binary builds for:
#      win-x64       (cross-compile only on Windows hosts)
#      osx-arm64     (Apple Silicon)
#      osx-x64       (Intel Macs)
#      linux-x64     (Ubuntu / Debian)
#
#  Usage:
#      ./publish.sh                # all RIDs the host can target
#      ./publish.sh osx-arm64      # single RID
# -----------------------------------------------------------------------------
set -euo pipefail

PROJECT="src/DeepRoot.Photino/DeepRoot.Photino.csproj"
OUT_ROOT="artifacts/deeproot"
CONFIG="Release"

publish_rid() {
    local rid="$1"
    echo "▶  Publishing $rid …"
    dotnet publish "$PROJECT" \
        --configuration "$CONFIG" \
        --runtime "$rid" \
        --self-contained true \
        --output "$OUT_ROOT/$rid" \
        -p:PublishAot=true \
        -p:OptimizationPreference=Size \
        -p:StripSymbols=true \
        -p:InvariantGlobalization=true \
        -p:DebugType=none \
        -p:DebugSymbols=false
    echo "✔  $rid → $OUT_ROOT/$rid"
}

if [[ $# -gt 0 ]]; then
    publish_rid "$1"
else
    # Native AOT can only target the *current* OS family.
    case "$(uname -s)" in
        Darwin)  publish_rid "osx-arm64"; publish_rid "osx-x64" ;;
        Linux)   publish_rid "linux-x64" ;;
        MINGW*|MSYS*|CYGWIN*) publish_rid "win-x64" ;;
        *)       echo "Unsupported host OS: $(uname -s)"; exit 1 ;;
    esac
fi
