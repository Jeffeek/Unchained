#!/usr/bin/env bash
# Download a FreeType2 native binary into src/Unchained.Drawing.Runtimes/runtimes/<rid>/native/.
#
# Usage:
#   bash scripts/FetchNatives/fetch-natives.sh [--rid <rid>]
#
#   If --rid is omitted the script auto-detects the current host RID.
#
# Supported RIDs and their sources:
#   win-x64 / win-arm64  : ubawurinna/freetype-windows-binaries (GitHub, MIT)
#                          Can be fetched from any host platform.
#   linux-x64            : system libfreetype (apt); host must be Linux x64.
#   linux-arm64          : system libfreetype (apt); host must be Linux arm64.
#   linux-musl-x64       : system libfreetype (apk); host must be Alpine x64.
#   linux-musl-arm64     : system libfreetype (apk); host must be Alpine arm64.
#   osx-x64              : Homebrew freetype; host must be macOS x64.
#   osx-arm64            : Homebrew freetype; host must be macOS arm64.
#
# Examples:
#   bash scripts/FetchNatives/fetch-natives.sh
#   bash scripts/FetchNatives/fetch-natives.sh --rid win-x64
#   bash scripts/FetchNatives/fetch-natives.sh --rid linux-arm64

set -euo pipefail

# ── Argument parsing ──────────────────────────────────────────────────────────

RID=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --rid) RID="$2"; shift 2 ;;
        -h|--help)
            grep -E '^#' "$0" | sed 's/^# \{0,1\}//'
            exit 0 ;;
        *) echo "Unknown argument: $1" >&2; exit 1 ;;
    esac
done

# ── Helpers ───────────────────────────────────────────────────────────────────

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd -- "${script_dir}/../.." && pwd)"
runtimes="${repo_root}/src/Unchained.Drawing.Runtimes/runtimes"

# Pinned commit — ubawurinna/freetype-windows-binaries (MIT).
# Update when upgrading FreeType.
WINDOWS_SHA="2fd97db170b19a9dda26131a784707611b9a4da1"
WINDOWS_BASE="https://github.com/ubawurinna/freetype-windows-binaries/raw/${WINDOWS_SHA}"

detect_host_rid() {
    local os arch
    case "$(uname -s)" in
        Linux*)
            os="linux"
            # Distinguish glibc vs musl
            if ldd --version 2>&1 | grep -qi musl; then os="linux-musl"; fi
            ;;
        Darwin*) os="osx" ;;
        MINGW*|MSYS*|CYGWIN*) os="win" ;;
        *) echo "Unsupported host OS: $(uname -s)" >&2; exit 1 ;;
    esac
    case "$(uname -m)" in
        x86_64|amd64)  arch="x64"   ;;
        aarch64|arm64) arch="arm64" ;;
        *) echo "Unsupported host arch: $(uname -m)" >&2; exit 1 ;;
    esac
    echo "${os}-${arch}"
}

download() {
    local url="$1" dest="$2"
    if command -v curl >/dev/null 2>&1; then
        curl -fsSL "$url" -o "$dest"
    else
        wget -q -O "$dest" "$url"
    fi
}

# ── Per-RID fetch functions ───────────────────────────────────────────────────

fetch_windows() {
    local rid="$1"  # win-x64 or win-arm64
    local arch="${rid#win-}"   # x64 or arm64
    local dest="${runtimes}/${rid}/native"
    mkdir -p "$dest"

    local url="${WINDOWS_BASE}/release%20dll/${arch}/freetype.dll"
    echo "[${rid}] downloading ${url}"
    download "$url" "${dest}/freetype6.dll"
    echo "[${rid}] -> ${dest}/freetype6.dll"
}

fetch_linux() {
    local rid="$1"  # linux-x64, linux-arm64, linux-musl-x64, linux-musl-arm64
    local dest="${runtimes}/${rid}/native"
    mkdir -p "$dest"

    local host_os
    host_os="$(uname -s)"
    if [[ "${host_os}" != "Linux" ]]; then
        echo "[${rid}] ERROR: fetching Linux libraries requires a Linux host (current: ${host_os})." >&2
        echo "       Run this script on a Linux machine with the target architecture." >&2
        exit 1
    fi

    local triplet
    case "${rid}" in
        linux-x64)        triplet="x86_64-linux-gnu"   ;;
        linux-arm64)      triplet="aarch64-linux-gnu"  ;;
        linux-musl-x64)   triplet=""                   ;;  # Alpine: no triplet
        linux-musl-arm64) triplet=""                   ;;
    esac

    local src=""

    if [[ "${rid}" == linux-musl-* ]]; then
        # Alpine Linux
        if command -v apk >/dev/null 2>&1; then
            apk add --no-cache freetype 2>/dev/null || true
        fi
        for cand in /usr/lib/libfreetype.so.6 /lib/libfreetype.so.6; do
            [[ -f "$cand" ]] && src="$cand" && break
        done
    else
        # Glibc Linux
        if command -v apt-get >/dev/null 2>&1; then
            apt-get install -y --no-install-recommends libfreetype6 2>/dev/null || true
        fi
        for cand in \
            "/usr/lib/${triplet}/libfreetype.so.6" \
            "/usr/lib64/libfreetype.so.6" \
            "/usr/lib/libfreetype.so.6"; do
            [[ -f "$cand" ]] && src="$cand" && break
        done
    fi

    if [[ -z "$src" ]]; then
        echo "[${rid}] ERROR: libfreetype.so.6 not found." >&2
        echo "       Install it: sudo apt-get install -y libfreetype6" >&2
        exit 1
    fi

    cp -f "$src" "${dest}/libfreetype.so.6"
    echo "[${rid}] -> ${dest}/libfreetype.so.6  (from ${src})"
}

fetch_osx() {
    local rid="$1"  # osx-x64 or osx-arm64
    local dest="${runtimes}/${rid}/native"
    mkdir -p "$dest"

    local host_os
    host_os="$(uname -s)"
    if [[ "${host_os}" != "Darwin" ]]; then
        echo "[${rid}] ERROR: fetching macOS libraries requires a macOS host (current: ${host_os})." >&2
        echo "       Run this script on a macOS machine with the target architecture." >&2
        exit 1
    fi

    if ! command -v brew >/dev/null 2>&1; then
        echo "[${rid}] ERROR: Homebrew not found." >&2
        echo "       Install Homebrew and run: brew install freetype" >&2
        exit 1
    fi

    local prefix
    prefix="$(brew --prefix freetype 2>/dev/null || true)"
    if [[ -z "$prefix" || ! -f "${prefix}/lib/libfreetype.6.dylib" ]]; then
        echo "[${rid}] freetype not installed — running: brew install freetype" >&2
        brew install freetype
        prefix="$(brew --prefix freetype)"
    fi

    cp -f "${prefix}/lib/libfreetype.6.dylib" "${dest}/libfreetype.6.dylib"
    echo "[${rid}] -> ${dest}/libfreetype.6.dylib  (from ${prefix}/lib/)"
}

# ── Main ──────────────────────────────────────────────────────────────────────

if [[ -z "$RID" ]]; then
    RID="$(detect_host_rid)"
    echo "Auto-detected RID: ${RID}"
fi

case "$RID" in
    win-x64|win-arm64)           fetch_windows "$RID" ;;
    linux-x64|linux-arm64)       fetch_linux   "$RID" ;;
    linux-musl-x64|linux-musl-arm64) fetch_linux "$RID" ;;
    osx-x64|osx-arm64)           fetch_osx     "$RID" ;;
    *) echo "Unknown RID: '${RID}'. Supported: win-x64 win-arm64 linux-x64 linux-arm64 linux-musl-x64 linux-musl-arm64 osx-x64 osx-arm64" >&2; exit 1 ;;
esac

# Verify output
native_dir="${runtimes}/${RID}/native"
count=$(find "${native_dir}" -not -name ".gitkeep" -type f 2>/dev/null | wc -l | tr -d ' ')
if [[ "${count}" -eq 0 ]]; then
    echo "[${RID}] ERROR: no binary produced under ${native_dir}" >&2
    exit 1
fi

echo
echo "Done."
