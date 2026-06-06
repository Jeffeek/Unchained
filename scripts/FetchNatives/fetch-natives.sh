#!/usr/bin/env bash
# Fetch native binaries required by each Unchained runtime package.
#
# Usage:
#   bash scripts/FetchNatives/fetch-natives.sh [--rid <rid>]
#
#   If --rid is omitted the script auto-detects the current host RID.
#
# Supported RIDs:
#   win-x64 / win-arm64       (downloaded from GitHub; works on any host)
#   linux-x64 / linux-arm64   (copied from system libfreetype via apt)
#   linux-musl-x64 / linux-musl-arm64  (copied from Alpine libfreetype via apk)
#   osx-x64 / osx-arm64       (copied from Homebrew freetype)
#
# To add a new library: add a fetch_<library> function below.
# To add a new package: add a fetch_<package>_runtimes function that calls the
#   relevant library fetchers with the package's runtimes/ directory.
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

download() {
    local url="$1" dest="$2"
    if command -v curl >/dev/null 2>&1; then
        curl -fsSL "$url" -o "$dest"
    else
        wget -q -O "$dest" "$url"
    fi
}

verify_output() {
    local rid="$1" native_dir="$2"
    local count
    count=$(find "${native_dir}" -not -name ".gitkeep" -type f 2>/dev/null | wc -l | tr -d ' ')
    if [[ "${count}" -eq 0 ]]; then
        echo "[${rid}] ERROR: no binary produced under ${native_dir}" >&2
        exit 1
    fi
}

# ── Library: FreeType2 ────────────────────────────────────────────────────────
#
# fetch_freetype <rid> <runtimes_dir>
#   Downloads / copies libfreetype into <runtimes_dir>/<rid>/native/.

# Pinned commit — ubawurinna/freetype-windows-binaries (MIT).
# Update this SHA when upgrading FreeType.
_FT_WIN_SHA="2fd97db170b19a9dda26131a784707611b9a4da1"
_FT_WIN_BASE="https://github.com/ubawurinna/freetype-windows-binaries/raw/${_FT_WIN_SHA}"

_ft_windows() {
    local rid="$1" runtimes="$2"
    local arch="${rid#win-}"
    local dest="${runtimes}/${rid}/native"
    mkdir -p "$dest"
    local url="${_FT_WIN_BASE}/release%20dll/${arch}/freetype.dll"
    echo "[${rid}] downloading ${url}"
    download "$url" "${dest}/freetype6.dll"
    echo "[${rid}] -> ${dest}/freetype6.dll"
}

_ft_linux() {
    local rid="$1" runtimes="$2"
    local dest="${runtimes}/${rid}/native"
    mkdir -p "$dest"
    if [[ "$(uname -s)" != "Linux" ]]; then
        echo "[${rid}] ERROR: Linux libraries require a Linux host." >&2; exit 1
    fi
    local src=""
    if [[ "${rid}" == linux-musl-* ]]; then
        command -v apk >/dev/null 2>&1 && apk add --no-cache freetype 2>/dev/null || true
        for cand in /usr/lib/libfreetype.so.6 /lib/libfreetype.so.6; do
            [[ -f "$cand" ]] && src="$cand" && break
        done
    else
        local triplet
        case "${rid}" in
            linux-x64)   triplet="x86_64-linux-gnu"  ;;
            linux-arm64) triplet="aarch64-linux-gnu" ;;
        esac
        command -v apt-get >/dev/null 2>&1 && \
            apt-get install -y --no-install-recommends libfreetype6 2>/dev/null || true
        for cand in \
                "/usr/lib/${triplet}/libfreetype.so.6" \
                "/usr/lib64/libfreetype.so.6" \
                "/usr/lib/libfreetype.so.6"; do
            [[ -f "$cand" ]] && src="$cand" && break
        done
    fi
    [[ -z "$src" ]] && { echo "[${rid}] ERROR: libfreetype.so.6 not found." >&2; exit 1; }
    cp -f "$src" "${dest}/libfreetype.so.6"
    echo "[${rid}] -> ${dest}/libfreetype.so.6  (from ${src})"
}

_ft_osx() {
    local rid="$1" runtimes="$2"
    local dest="${runtimes}/${rid}/native"
    mkdir -p "$dest"
    if [[ "$(uname -s)" != "Darwin" ]]; then
        echo "[${rid}] ERROR: macOS libraries require a macOS host." >&2; exit 1
    fi
    command -v brew >/dev/null 2>&1 || { echo "[${rid}] ERROR: Homebrew not found." >&2; exit 1; }
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

fetch_freetype() {
    local rid="$1" runtimes="$2"
    case "$rid" in
        win-x64|win-arm64)               _ft_windows "$rid" "$runtimes" ;;
        linux-x64|linux-arm64)           _ft_linux   "$rid" "$runtimes" ;;
        linux-musl-x64|linux-musl-arm64) _ft_linux   "$rid" "$runtimes" ;;
        osx-x64|osx-arm64)              _ft_osx     "$rid" "$runtimes" ;;
        *) echo "Unknown RID: '${rid}'" >&2; exit 1 ;;
    esac
}

# ── Package fetch functions ───────────────────────────────────────────────────
#
# Each function fetches everything a specific runtime package needs.
# Add new library fetcher calls here when a package gains a new dependency.

fetch_drawing_runtimes() {
    local rid="$1"
    local runtimes="${repo_root}/src/Unchained.Drawing.Runtimes/runtimes"
    fetch_freetype "$rid" "$runtimes"
    verify_output  "$rid" "${runtimes}/${rid}/native"
}

# fetch_pdf_runtimes() {
#     local rid="$1"
#     local runtimes="${repo_root}/src/Unchained.Pdf.Runtimes/runtimes"
#     # Add library fetchers here if Pdf.Runtimes ever needs its own binaries.
# }

# fetch_pptx_runtimes() {
#     local rid="$1"
#     local runtimes="${repo_root}/src/Unchained.Pptx.Runtimes/runtimes"
#     # Add library fetchers here if Pptx.Runtimes ever needs its own binaries.
# }

# ── Main ──────────────────────────────────────────────────────────────────────

if [[ -z "$RID" ]]; then
    RID="$(detect_host_rid)"
    echo "Auto-detected RID: ${RID}"
fi

fetch_drawing_runtimes "$RID"

echo
echo "Done."
