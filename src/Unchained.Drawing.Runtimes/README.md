# Unchained.Drawing.Runtimes

Native FreeType2 runtime binary for **linux-arm64** — the one platform the FreeTypeSharp NuGet package does not bundle. This package contains no managed code.

**License:** MIT (package) / FTL (FreeType2 binary)

---

## You do not need to install this package directly

`Unchained.Drawing.Runtimes` is an automatic transitive dependency of any Unchained rendering package. FreeTypeSharp supplies the FreeType2 binary for Windows, macOS, and linux-x64; this package fills the linux-arm64 gap. The correct binary is selected automatically per platform.

```xml
<!-- Install either of these — Runtimes is pulled in automatically -->
<PackageReference Include="Unchained.Pdf.Rendering" Version="0.1.0" />
<PackageReference Include="Unchained.Pptx.Rendering" Version="0.1.0" />
```

---

## What's inside

| Platform | RID | Binary |
|---|---|---|
| Linux arm64 | `linux-arm64` | `libfreetype.so` |

Every other platform's FreeType2 binary ships inside the
[FreeTypeSharp](https://www.nuget.org/packages/FreeTypeSharp) package
(win-x64/arm64/x86, linux-x64, macOS), so this package carries linux-arm64 only.

---

## Library name resolution

FreeTypeSharp registers a `NativeLibrary.SetDllImportResolver` that probes
`runtimes/linux-arm64/native/libfreetype.so` (no version suffix) before falling back to a
system-installed FreeType2 (`/usr/lib`, `/usr/local/lib`). The binary in this package is
named to match that probe path.

---

## FreeType2

[FreeType2](https://freetype.org) is a freely available, high-quality font rendering library used by the Unchained rendering packages to rasterize TrueType, OpenType, Type 1, and CFF outlines into pixel bitmaps.

FreeType2 is licensed under the [FreeType License (FTL)](https://freetype.org/license.html), a BSD-style permissive license that allows free use in commercial and open-source products.

---

## For developers building from source

The linux-arm64 binary is **not committed** to the repository. To populate it (only needed
when packing for linux-arm64), run on a Linux host:

```bash
bash scripts/FetchNatives/fetch-natives.sh --rid linux-arm64
```

On every other platform the fetch script is a no-op — FreeTypeSharp already provides the
binary. Source: the system package (`apt-get install libfreetype6`).

---

[GitHub](https://github.com/Jeffeek/Unchained) · [Report an issue](https://github.com/Jeffeek/Unchained/issues)
