# Unchained.Drawing.Runtimes

Native FreeType2 runtime binaries for the Unchained rendering stack. This package contains no managed code — it is a pure native binary carrier that bundles the FreeType2 shared library for all supported platforms.

**License:** MIT (package) / FTL (FreeType2 binaries)

---

## You do not need to install this package directly

`Unchained.Drawing.Runtimes` is an automatic transitive dependency of any Unchained rendering package. The .NET SDK selects and deploys the correct platform binary automatically.

```xml
<!-- Install either of these — Runtimes is pulled in automatically -->
<PackageReference Include="Unchained.Pdf.Rendering" Version="0.1.0" />
<PackageReference Include="Unchained.Pptx.Rendering" Version="0.1.0" />
```

---

## What's inside

| Platform | RID | Binary |
|---|---|---|
| Windows x64 | `win-x64` | `freetype6.dll` |
| Windows arm64 | `win-arm64` | `freetype6.dll` |
| Linux x64 | `linux-x64` | `libfreetype.so.6` |
| Linux arm64 | `linux-arm64` | `libfreetype.so.6` |
| macOS x64 (Intel) | `osx-x64` | `libfreetype.6.dylib` |
| macOS arm64 (Apple Silicon) | `osx-arm64` | `libfreetype.6.dylib` |

The binary for your current runtime is copied next to your application output at build time. The others remain in the NuGet cache and are not deployed.

---

## Library name mapping

SharpFont uses `[DllImport("freetype6")]` on all platforms. A `NativeLibrary.SetDllImportResolver` registered at module load time maps the logical name to the platform-specific filename and locates it in the output directory:

| OS | Resolved file |
|---|---|
| Windows | `runtimes/win-x64/native/freetype6.dll` |
| Linux | `runtimes/linux-x64/native/libfreetype.so.6` |
| macOS | `runtimes/osx-arm64/native/libfreetype.6.dylib` |

If the bundled copy is absent, the resolver falls back to the system-installed FreeType2.

---

## FreeType2

[FreeType2](https://freetype.org) is a freely available, high-quality font rendering library used by the Unchained rendering packages to rasterize TrueType, OpenType, Type 1, and CFF outlines into pixel bitmaps.

FreeType2 is licensed under the [FreeType License (FTL)](https://freetype.org/license.html), a BSD-style permissive license that allows free use in commercial and open-source products.

---

## For developers building from source

Native binaries are **not committed** to the repository. Before building locally, run the fetch script for your platform:

```bash
# Linux / macOS (Git Bash on Windows also works — handles all RIDs)
bash scripts/FetchNatives/fetch-natives.sh

# Windows PowerShell
pwsh scripts/FetchNatives/fetch-natives.ps1

# Download a specific RID from any host
bash scripts/FetchNatives/fetch-natives.sh --rid win-x64
pwsh scripts/FetchNatives/fetch-natives.ps1 -Rid osx-arm64
```

Sources per platform:
- **Windows** — [ubawurinna/freetype-windows-binaries](https://github.com/ubawurinna/freetype-windows-binaries) (MIT, downloaded via script)
- **Linux** — system package (`apt-get install libfreetype6`)
- **macOS** — Homebrew (`brew install freetype`)

---

[GitHub](https://github.com/Jeffeek/Unchained) · [Report an issue](https://github.com/Jeffeek/Unchained/issues)
