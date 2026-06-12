#!/usr/bin/env bash
# Fetch native binaries required by Unchained.Drawing.Runtimes.
#
# Usage:
#   bash scripts/FetchNatives/fetch-natives.sh [--rid <rid>]
#
#   If --rid is omitted the script auto-detects the current host RID.
#
# The FreeType2 native library is supplied by the FreeTypeSharp NuGet package for
# Windows (x64/arm64/x86), macOS, and linux-x64. The ONLY platform FreeTypeSharp does
# not bundle is linux-arm64, so that is the only binary this script fetches.
#
# The file is named libfreetype.so (no version suffix) to match FreeTypeSharp's
# DllImport resolver, which probes runtimes/linux-arm64/native/libfreetype.so.
#
# For any RID other than linux-arm64 the script is a no-op (FreeTypeSharp already
# provides it; on linux-arm64 a system-installed FreeType2 also works as a fallback).
#
# Examples:
#   bash scripts/FetchNatives/fetch-natives.sh
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

# ── Shared helpers ────────────────────────────────────────────────────────────

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd -- "${script_dir}/../.." && pwd)"

detect_host_rid() {
    local os arch
    case "$(uname -s)" in
        Linux*)
            os="linux"
            if ldd --version 2>&1 | grep -qi musl; then os="linux-musl"; fi
            ;;
        Darwin*)         os="osx" ;;
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

# ── Library: FreeType2 (linux-arm64 only) ──────────────────────────────────────
#
# FreeTypeSharp bundles every other platform. Only linux-arm64 must be supplied by
# Unchained.Drawing.Runtimes, named libfreetype.so to match FreeTypeSharp's resolver.

_ft_linux_arm64() {
    local runtimes="$1"
    local dest="${runtimes}/linux-arm64/native"
    if [[ "$(uname -s)" != "Linux" ]]; then
        echo "[linux-arm64] ERROR: Linux libraries require a Linux host." >&2; exit 1
    fi
    mkdir -p "$dest"

    command -v apt-get >/dev/null 2>&1 && \
        apt-get install -y --no-install-recommends libfreetype6 2>/dev/null || true
    local src=""
    for cand in \
            "/usr/lib/aarch64-linux-gnu/libfreetype.so.6" \
            "/usr/lib64/libfreetype.so.6" \
            "/usr/lib/libfreetype.so.6"; do
        [[ -f "$cand" ]] && src="$cand" && break
    done
    [[ -z "$src" ]] && { echo "[linux-arm64] ERROR: libfreetype.so.6 not found." >&2; exit 1; }

    # FreeTypeSharp's resolver looks for libfreetype.so (no version suffix).
    cp -f "$src" "${dest}/libfreetype.so"
    echo "[linux-arm64] -> ${dest}/libfreetype.so  (from ${src})"
}

# ── Main ──────────────────────────────────────────────────────────────────────

if [[ -z "$RID" ]]; then
    RID="$(detect_host_rid)"
    echo "Auto-detected RID: ${RID}"
fi

if [[ "$RID" == "linux-arm64" ]]; then
    _ft_linux_arm64 "${repo_root}/src/Unchained.Drawing.Runtimes/runtimes"
else
    echo "[${RID}] FreeType2 is provided by the FreeTypeSharp package — nothing to fetch."
fi

echo
echo "Done."
