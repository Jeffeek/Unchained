#Requires -Version 7
<#
.SYNOPSIS
    Fetches native binaries required by Unchained.Drawing.Runtimes.
.DESCRIPTION
    The FreeType2 native library is supplied by the FreeTypeSharp NuGet package for
    Windows (x64/arm64/x86), macOS, and linux-x64. The ONLY platform FreeTypeSharp does
    not bundle is linux-arm64, so that is the only binary this script fetches into
    Unchained.Drawing.Runtimes.

    The file is named libfreetype.so (no version suffix) to match FreeTypeSharp's
    DllImport resolver, which probes runtimes/linux-arm64/native/libfreetype.so.

    If -Rid is omitted the script auto-detects the current host RID. For any RID other
    than linux-arm64 the script is a no-op (FreeTypeSharp already provides it, or — on
    linux-arm64 hosts in containers — a system-installed FreeType2 also works).
.EXAMPLE
    pwsh scripts/FetchNatives/fetch-natives.ps1
    pwsh scripts/FetchNatives/fetch-natives.ps1 -Rid linux-arm64
#>
param(
    [string] $Rid = ""
)

$ErrorActionPreference = 'Stop'

# ── Shared helpers ────────────────────────────────────────────────────────────

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')

function Get-HostRid {
    $os = if ($IsWindows) { 'win' }
          elseif ($IsLinux) {
              $musl = (ldd --version 2>&1) -match 'musl'
              if ($musl) { 'linux-musl' } else { 'linux' }
          }
          elseif ($IsMacOS) { 'osx' }
          else { throw "Unsupported host OS" }

    $arch = switch ([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture) {
        'X64'   { 'x64' }
        'Arm64' { 'arm64' }
        default { throw "Unsupported host architecture: $_" }
    }
    return "$os-$arch"
}

# ── Library: FreeType2 (linux-arm64 only) ──────────────────────────────────────
#
# FreeTypeSharp bundles every other platform. Only linux-arm64 must be supplied by
# Unchained.Drawing.Runtimes, named libfreetype.so to match FreeTypeSharp's resolver.

function _FT_LinuxArm64([string]$Runtimes) {
    if (-not $IsLinux) { throw "[linux-arm64] ERROR: Linux libraries require a Linux host." }
    $dest = Join-Path $Runtimes "linux-arm64\native"
    New-Item -ItemType Directory -Force $dest | Out-Null

    try { & apt-get install -y --no-install-recommends libfreetype6 2>$null } catch {}
    $src = $null
    foreach ($cand in @('/usr/lib/aarch64-linux-gnu/libfreetype.so.6', '/usr/lib64/libfreetype.so.6', '/usr/lib/libfreetype.so.6')) {
        if (Test-Path $cand) { $src = $cand; break }
    }
    if (-not $src) { throw "[linux-arm64] ERROR: libfreetype.so.6 not found." }

    # FreeTypeSharp's resolver looks for libfreetype.so (no version suffix).
    Copy-Item -Force $src (Join-Path $dest 'libfreetype.so')
    Write-Host "[linux-arm64] -> $dest\libfreetype.so  (from $src)"
}

# ── Main ──────────────────────────────────────────────────────────────────────

if (-not $Rid) {
    $Rid = Get-HostRid
    Write-Host "Auto-detected RID: $Rid"
}

if ($Rid -eq 'linux-arm64') {
    $runtimes = Join-Path $repoRoot 'src\Unchained.Drawing.Runtimes\runtimes'
    _FT_LinuxArm64 $runtimes
}
else {
    Write-Host "[$Rid] FreeType2 is provided by the FreeTypeSharp package — nothing to fetch."
}

Write-Host "`nDone."
