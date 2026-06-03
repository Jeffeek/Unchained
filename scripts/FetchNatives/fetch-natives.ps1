#Requires -Version 7
<#
.SYNOPSIS
    Downloads a FreeType2 native binary into src/Unchained.Pdf.Runtimes/runtimes/<rid>/native/.
.DESCRIPTION
    If -Rid is omitted the script auto-detects the current host RID.

    Supported RIDs and their sources:
      win-x64 / win-arm64  : ubawurinna/freetype-windows-binaries (GitHub, MIT)
                             Can be fetched from any host platform.
      linux-x64            : system libfreetype (apt); host must be Linux x64.
      linux-arm64          : system libfreetype (apt); host must be Linux arm64.
      linux-musl-x64       : system libfreetype (apk); host must be Alpine x64.
      linux-musl-arm64     : system libfreetype (apk); host must be Alpine arm64.
      osx-x64              : Homebrew freetype; host must be macOS x64.
      osx-arm64            : Homebrew freetype; host must be macOS arm64.
.EXAMPLE
    pwsh scripts/FetchNatives/fetch-natives.ps1
    pwsh scripts/FetchNatives/fetch-natives.ps1 -Rid win-x64
    pwsh scripts/FetchNatives/fetch-natives.ps1 -Rid linux-arm64
#>
param(
    [string] $Rid = ""
)

$ErrorActionPreference = 'Stop'

# ── Paths & constants ─────────────────────────────────────────────────────────

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$runtimes = Join-Path $repoRoot 'src\Unchained.Pdf.Runtimes\runtimes'

# Pinned commit — ubawurinna/freetype-windows-binaries (MIT).
# Update when upgrading FreeType.
$WindowsSha  = '2fd97db170b19a9dda26131a784707611b9a4da1'
$WindowsBase = "https://github.com/ubawurinna/freetype-windows-binaries/raw/$WindowsSha"

# ── Helpers ───────────────────────────────────────────────────────────────────

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

function Invoke-Download([string]$Url, [string]$Dest) {
    Write-Host "  GET $Url"
    Invoke-WebRequest -Uri $Url -OutFile $Dest -UseBasicParsing
}

# ── Per-RID fetch functions ───────────────────────────────────────────────────

function Fetch-Windows([string]$rid) {
    $arch = $rid -replace '^win-', ''   # x64 or arm64
    $dest = Join-Path $runtimes "$rid\native"
    New-Item -ItemType Directory -Force $dest | Out-Null

    $encodedPath = "release%20dll/$arch/freetype.dll"
    $url = "$WindowsBase/$encodedPath"
    Write-Host "[$rid] downloading $url"
    Invoke-Download $url (Join-Path $dest 'freetype6.dll')
    Write-Host "[$rid] -> $dest\freetype6.dll"
}

function Fetch-Linux([string]$rid) {
    $dest = Join-Path $runtimes "$rid\native"
    New-Item -ItemType Directory -Force $dest | Out-Null

    if (-not $IsLinux) {
        throw "[$rid] ERROR: fetching Linux libraries requires a Linux host. Run this script on Linux."
    }

    $isMusl = $rid -match 'musl'
    $src = $null

    if ($isMusl) {
        & apk add --no-cache freetype 2>$null
        foreach ($cand in @('/usr/lib/libfreetype.so.6', '/lib/libfreetype.so.6')) {
            if (Test-Path $cand) { $src = $cand; break }
        }
    } else {
        $triplet = if ($rid -eq 'linux-x64') { 'x86_64-linux-gnu' } else { 'aarch64-linux-gnu' }
        try { & apt-get install -y --no-install-recommends libfreetype6 2>$null } catch {}
        foreach ($cand in @("/usr/lib/$triplet/libfreetype.so.6", '/usr/lib64/libfreetype.so.6', '/usr/lib/libfreetype.so.6')) {
            if (Test-Path $cand) { $src = $cand; break }
        }
    }

    if (-not $src) {
        throw "[$rid] ERROR: libfreetype.so.6 not found. Run: sudo apt-get install -y libfreetype6"
    }

    Copy-Item -Force $src (Join-Path $dest 'libfreetype.so.6')
    Write-Host "[$rid] -> $dest\libfreetype.so.6  (from $src)"
}

function Fetch-OSX([string]$rid) {
    $dest = Join-Path $runtimes "$rid\native"
    New-Item -ItemType Directory -Force $dest | Out-Null

    if (-not $IsMacOS) {
        throw "[$rid] ERROR: fetching macOS libraries requires a macOS host. Run this script on macOS."
    }

    if (-not (Get-Command brew -ErrorAction SilentlyContinue)) {
        throw "[$rid] ERROR: Homebrew not found. Install Homebrew and run: brew install freetype"
    }

    $prefix = (brew --prefix freetype 2>$null) -join ''
    if (-not $prefix -or -not (Test-Path "$prefix/lib/libfreetype.6.dylib")) {
        Write-Host "[$rid] freetype not installed — running: brew install freetype"
        brew install freetype
        $prefix = (brew --prefix freetype) -join ''
    }

    Copy-Item -Force "$prefix/lib/libfreetype.6.dylib" (Join-Path $dest 'libfreetype.6.dylib')
    Write-Host "[$rid] -> $dest\libfreetype.6.dylib  (from $prefix/lib/)"
}

# ── Main ──────────────────────────────────────────────────────────────────────

if (-not $Rid) {
    $Rid = Get-HostRid
    Write-Host "Auto-detected RID: $Rid"
}

switch -Regex ($Rid) {
    '^win-(x64|arm64)$'              { Fetch-Windows $Rid }
    '^linux-(x64|arm64)$'            { Fetch-Linux   $Rid }
    '^linux-musl-(x64|arm64)$'       { Fetch-Linux   $Rid }
    '^osx-(x64|arm64)$'              { Fetch-OSX     $Rid }
    default {
        throw "Unknown RID: '$Rid'. Supported: win-x64 win-arm64 linux-x64 linux-arm64 linux-musl-x64 linux-musl-arm64 osx-x64 osx-arm64"
    }
}

# Verify
$nativeDir = Join-Path $runtimes "$Rid\native"
# Note: -Exclude on Get-ChildItem requires a wildcard in the path; use Where-Object instead.
$count = (Get-ChildItem "$nativeDir\*" -File -ErrorAction SilentlyContinue |
          Where-Object { $_.Name -ne '.gitkeep' } |
          Measure-Object).Count
if ($count -eq 0) {
    throw "[$Rid] ERROR: no binary produced under $nativeDir"
}

Write-Host "`nDone."
