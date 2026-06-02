# Unchained.Pdf.Runtimes

Contains **only** the FreeType2 native library binaries — one per supported platform.
No managed code or C# files are produced by this project.

## Bundled binaries

| Platform | File | Version | Source |
|---|---|---|---|
| `win-x64` | `freetype6.dll` | 2.5.5 | SharpFont.Dependencies 2.5.5 |
| `win-arm64` | `freetype6.dll` | latest | freetype-windows-binaries (GitHub) |
| `linux-x64` | `libfreetype.so.6` | 2.12.1 | Debian bookworm (libfreetype6) |
| `linux-arm64` | `libfreetype.so.6` | 2.12.1 | Debian bookworm (libfreetype6) |
| `linux-musl-x64` | `libfreetype.so.6` | 2.13.2 | Alpine Linux 3.19 |
| `linux-musl-arm64` | `libfreetype.so.6` | 2.13.2 | Alpine Linux 3.19 |
| `osx-x64` | `libfreetype.6.dylib` | 2.14.3 | Homebrew (freetype formula, sonoma bottle) |
| `osx-arm64` | `libfreetype.6.dylib` | 2.14.3 | Homebrew (freetype formula, arm64_sonoma bottle) |

## Name mapping

SharpFont uses `DllImport("freetype6")` on all platforms. `FontCache` installs a
`NativeLibrary.SetDllImportResolver` that maps this to the platform-specific filename:

| OS | Loaded as |
|---|---|
| Windows | `freetype6.dll` (default DllImport, no remapping) |
| Linux | `libfreetype.so.6` |
| macOS | `libfreetype.6.dylib` |

The resolver checks the application output directory first (bundled copy),
then falls back to the system library path — so system-installed FreeType2 also works.

## Updating a binary

Drop the new file into `runtimes/<rid>/native/` with the correct filename from the
table above. The `<Content>` items in the `.csproj` are already in place for all 8 RIDs.
