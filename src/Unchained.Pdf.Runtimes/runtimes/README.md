# Native FreeType2 binaries

This directory contains per-RID FreeType2 native binaries. They are copied
next to consuming assemblies at build time and packed into `Unchained.Pdf.Runtimes`
under `runtimes/<rid>/native/` (the standard .NET runtime-asset layout).

## Expected files

| RID                | File                    |
|--------------------|-------------------------|
| `win-x64`          | `freetype6.dll`         |
| `win-arm64`        | `freetype6.dll`         |
| `linux-x64`        | `libfreetype.so.6`      |
| `linux-arm64`      | `libfreetype.so.6`      |
| `linux-musl-x64`   | `libfreetype.so.6`      |
| `linux-musl-arm64` | `libfreetype.so.6`      |
| `osx-x64`          | `libfreetype.6.dylib`   |
| `osx-arm64`        | `libfreetype.6.dylib`   |

## Populating this directory

Binaries are **not** committed to git — each `native/` folder holds only a
`.gitkeep` placeholder. Run the fetch script for your platform before building:

```pwsh
# Windows
pwsh scripts/FetchNatives/fetch-natives.ps1
```

```bash
# Linux
sudo apt-get install -y libfreetype6
bash scripts/FetchNatives/fetch-natives.sh

# macOS
brew install freetype
bash scripts/FetchNatives/fetch-natives.sh
```

After fetching, `dotnet build` copies the binary to each consumer's output
directory and `dotnet pack` rolls it into the NuGet package.
