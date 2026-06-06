#Requires -Version 7
<#
.SYNOPSIS
    Fetches native binaries required by each Unchained runtime package.
.DESCRIPTION
    If -Rid is omitted the script auto-detects the current host RID.

    Supported RIDs:
      win-x64 / win-arm64       (downloaded from GitHub; works on any host)
      linux-x64 / linux-arm64   (copied from system libfreetype via apt)
      linux-musl-x64 / linux-musl-arm64  (copied from Alpine libfreetype via apk)
      osx-x64 / osx-arm64       (copied from Homebrew freetype)

    To add a new library: add a private Fetch-<Library> function below.
    To add a new package: add a Fetch-<Package>Runtimes function that calls the
      relevant library fetchers with the package's runtimes/ directory.
.EXAMPLE
    pwsh scripts/FetchNatives/fetch-natives.ps1
    pwsh scripts/FetchNatives/fetch-natives.ps1 -Rid win-x64
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

function Invoke-Download([string]$Url, [string]$Dest) {
    Write-Host "  GET $Url"
    Invoke-WebRequest -Uri $Url -OutFile $Dest -UseBasicParsing
}

function Assert-Output([string]$Rid, [string]$NativeDir) {
    $count = (Get-ChildItem "$NativeDir\*" -File -ErrorAction SilentlyContinue |
              Where-Object { $_.Name -ne '.gitkeep' } |
              Measure-Object).Count
    if ($count -eq 0) {
        throw "[$Rid] ERROR: no binary produced under $NativeDir"
    }
}

# ── Library: FreeType2 ────────────────────────────────────────────────────────
#
# Invoke-FetchFreeType <rid> <runtimes_dir>
#   Downloads / copies libfreetype into <runtimes_dir>\<rid>\native\.

# Pinned commit — ubawurinna/freetype-windows-binaries (MIT).
# Update this SHA when upgrading FreeType.
$script:FtWinSha  = '2fd97db170b19a9dda26131a784707611b9a4da1'
$script:FtWinBase = "https://github.com/ubawurinna/freetype-windows-binaries/raw/$($script:FtWinSha)"

function _FT_Windows([string]$Rid, [string]$Runtimes) {
    $arch = $Rid -replace '^win-', ''
    $dest = Join-Path $Runtimes "$Rid\native"
    New-Item -ItemType Directory -Force $dest | Out-Null
    $url = "$($script:FtWinBase)/release%20dll/$arch/freetype.dll"
    Write-Host "[$Rid] downloading $url"
    Invoke-Download $url (Join-Path $dest 'freetype6.dll')
    Write-Host "[$Rid] -> $dest\freetype6.dll"
}

function _FT_Linux([string]$Rid, [string]$Runtimes) {
    $dest = Join-Path $Runtimes "$Rid\native"
    New-Item -ItemType Directory -Force $dest | Out-Null
    if (-not $IsLinux) { throw "[$Rid] ERROR: Linux libraries require a Linux host." }
    $isMusl = $Rid -match 'musl'
    $src = $null
    if ($isMusl) {
        & apk add --no-cache freetype 2>$null
        foreach ($cand in @('/usr/lib/libfreetype.so.6', '/lib/libfreetype.so.6')) {
            if (Test-Path $cand) { $src = $cand; break }
        }
    } else {
        $triplet = if ($Rid -eq 'linux-x64') { 'x86_64-linux-gnu' } else { 'aarch64-linux-gnu' }
        try { & apt-get install -y --no-install-recommends libfreetype6 2>$null } catch {}
        foreach ($cand in @("/usr/lib/$triplet/libfreetype.so.6", '/usr/lib64/libfreetype.so.6', '/usr/lib/libfreetype.so.6')) {
            if (Test-Path $cand) { $src = $cand; break }
        }
    }
    if (-not $src) { throw "[$Rid] ERROR: libfreetype.so.6 not found." }
    Copy-Item -Force $src (Join-Path $dest 'libfreetype.so.6')
    Write-Host "[$Rid] -> $dest\libfreetype.so.6  (from $src)"
}

function _FT_OSX([string]$Rid, [string]$Runtimes) {
    $dest = Join-Path $Runtimes "$Rid\native"
    New-Item -ItemType Directory -Force $dest | Out-Null
    if (-not $IsMacOS) { throw "[$Rid] ERROR: macOS libraries require a macOS host." }
    if (-not (Get-Command brew -ErrorAction SilentlyContinue)) {
        throw "[$Rid] ERROR: Homebrew not found."
    }
    $prefix = (brew --prefix freetype 2>$null) -join ''
    if (-not $prefix -or -not (Test-Path "$prefix/lib/libfreetype.6.dylib")) {
        Write-Host "[$Rid] freetype not installed — running: brew install freetype"
        brew install freetype
        $prefix = (brew --prefix freetype) -join ''
    }
    Copy-Item -Force "$prefix/lib/libfreetype.6.dylib" (Join-Path $dest 'libfreetype.6.dylib')
    Write-Host "[$Rid] -> $dest\libfreetype.6.dylib  (from $prefix/lib/)"
}

function Invoke-FetchFreeType([string]$Rid, [string]$Runtimes) {
    switch -Regex ($Rid) {
        '^win-(x64|arm64)$'              { _FT_Windows $Rid $Runtimes }
        '^linux-(x64|arm64)$'            { _FT_Linux   $Rid $Runtimes }
        '^linux-musl-(x64|arm64)$'       { _FT_Linux   $Rid $Runtimes }
        '^osx-(x64|arm64)$'              { _FT_OSX     $Rid $Runtimes }
        default { throw "Unknown RID: '$Rid'. Supported: win-x64 win-arm64 linux-x64 linux-arm64 linux-musl-x64 linux-musl-arm64 osx-x64 osx-arm64" }
    }
}

# ── Package fetch functions ───────────────────────────────────────────────────
#
# Each function fetches everything a specific runtime package needs.
# Add new library fetcher calls here when a package gains a new dependency.

function Fetch-DrawingRuntimes([string]$Rid) {
    $runtimes = Join-Path $repoRoot 'src\Unchained.Drawing.Runtimes\runtimes'
    Invoke-FetchFreeType $Rid $runtimes
    Assert-Output $Rid (Join-Path $runtimes "$Rid\native")
}

# function Fetch-PdfRuntimes([string]$Rid) {
#     $runtimes = Join-Path $repoRoot 'src\Unchained.Pdf.Runtimes\runtimes'
#     # Add library fetchers here if Pdf.Runtimes ever needs its own binaries.
# }

# function Fetch-PptxRuntimes([string]$Rid) {
#     $runtimes = Join-Path $repoRoot 'src\Unchained.Pptx.Runtimes\runtimes'
#     # Add library fetchers here if Pptx.Runtimes ever needs its own binaries.
# }

# ── Main ──────────────────────────────────────────────────────────────────────

if (-not $Rid) {
    $Rid = Get-HostRid
    Write-Host "Auto-detected RID: $Rid"
}

Fetch-DrawingRuntimes $Rid

Write-Host "`nDone."
