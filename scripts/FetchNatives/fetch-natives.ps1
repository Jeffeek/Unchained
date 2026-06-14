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

    Every materialized binary's SHA-256 is computed and printed. If checksums.sha256 has a
    pinned hash for the RID, the binary is verified against it and the script throws on
    mismatch. If no pin exists, the script warns. Use -PinChecksum on a trusted host to
    record the current hash, then commit + review the diff.
.EXAMPLE
    pwsh scripts/FetchNatives/fetch-natives.ps1
    pwsh scripts/FetchNatives/fetch-natives.ps1 -Rid linux-arm64
    pwsh scripts/FetchNatives/fetch-natives.ps1 -Rid linux-arm64 -PinChecksum
#>
param(
    [string] $Rid = "",
    [switch] $PinChecksum
)

$ErrorActionPreference = 'Stop'

# ── Shared helpers ────────────────────────────────────────────────────────────

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$checksumsFile = Join-Path $PSScriptRoot 'checksums.sha256'

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

# ── Checksum helpers ────────────────────────────────────────────────────────────
#
# Compute the SHA-256 of a file, look up its pinned value in checksums.sha256, and
# either verify (throw on mismatch), warn (no pin), or record it (-PinChecksum).

function Get-PinnedHash([string]$RidKey) {
    if (-not (Test-Path $checksumsFile)) { return $null }
    foreach ($line in Get-Content $checksumsFile) {
        $t = $line.Trim()
        if ($t -eq '' -or $t.StartsWith('#')) { continue }
        $parts = $t -split '\s+', 2
        if ($parts.Count -eq 2 -and $parts[0] -eq $RidKey) { return $parts[1].Trim() }
    }
    return $null
}

function Set-PinnedHash([string]$RidKey, [string]$Hash) {
    $lines = if (Test-Path $checksumsFile) { Get-Content $checksumsFile } else { @() }
    $kept = $lines | Where-Object { ($_ -split '\s+', 2)[0] -ne $RidKey }
    $kept += ('{0,-16} {1}' -f $RidKey, $Hash)
    Set-Content -Path $checksumsFile -Value $kept
    Write-Host "[$RidKey] pinned SHA-256 recorded in $checksumsFile"
}

function Confirm-Checksum([string]$RidKey, [string]$Path) {
    $actual = (Get-FileHash -Algorithm SHA256 -Path $Path).Hash.ToLowerInvariant()
    Write-Host "[$RidKey] SHA-256: $actual"

    if ($PinChecksum) { Set-PinnedHash $RidKey $actual; return }

    $pinned = Get-PinnedHash $RidKey
    if (-not $pinned) {
        Write-Warning "[$RidKey] no pinned checksum. To pin, run with -PinChecksum on a trusted host."
    }
    elseif ($pinned.ToLowerInvariant() -ne $actual) {
        throw "[$RidKey] checksum mismatch! expected $pinned actual $actual"
    }
    else {
        Write-Host "[$RidKey] checksum verified against pin."
    }
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
    Confirm-Checksum "linux-arm64" (Join-Path $dest 'libfreetype.so')
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
