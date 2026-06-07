# Unchained.Pptx.Rendering

Slide rasterization for `Unchained.Pptx`. Renders PPTX slides to PNG (or other image formats) using FreeType2 for font rasterization and HarfBuzz for text shaping.

## Installation

```xml
<PackageReference Include="Unchained.Pptx.Rendering" />
```

Before building or running, fetch the FreeType2 native library for your platform:

```bash
# Linux / macOS / Windows (Git Bash) — auto-detects host RID
bash scripts/FetchNatives/fetch-natives.sh

# Or target a specific RID
bash scripts/FetchNatives/fetch-natives.sh --rid win-x64

# Windows (PowerShell)
pwsh scripts/FetchNatives/fetch-natives.ps1
```

If the native library is missing, rendering calls throw `DllNotFoundException`
(`Unable to load DLL 'freetype6'`). The fetch script copies FreeType into
`Unchained.Drawing.Runtimes`, which the rendering stack depends on transitively.

## Quick start

```csharp
using Unchained.Pptx.Engine;
using Unchained.Pptx.Rendering;
using Unchained.Pptx.Rendering.Engine;

var processor = new PresentationProcessor();
var doc = await processor.LoadAsync("presentation.pptx");

// Render all slides at 1920×1080 (default)
var images = await SlideRenderer.RenderAllAsync(doc);

// Save each slide as PNG
for (var i = 0; i < images.Length; i++)
    await images[i].SaveAsync($"slide{i + 1}.png");

// Render a single slide at custom resolution
var thumbnail = await SlideRenderer.RenderAsync(
    doc.Slides[0],
    doc.SlideSize,
    new RenderOptions { WidthPx = 640, HeightPx = 360 });
```

## Output formats

`RenderOptions.Format` selects the encoder:

| Format | Status | Notes |
|---|---|---|
| `RenderImageFormat.Png` (default) | Supported | Lossless, recommended |
| `RenderImageFormat.Bmp` | Supported | Uncompressed 24-bit, large files |
| `RenderImageFormat.Jpeg` | Not implemented | Throws `NotSupportedException` |

The returned `PptxImage.Format` always matches the bytes in `PptxImage.Data`.

> Note: embedded raster images inside slides (`p:pic`) are not yet decoded by the
> rasterizer — their region is left transparent. Text, shapes, and solid fills render.

## Dependencies

| Library | License | Purpose |
|---|---|---|
| HarfBuzzSharp | MIT | Unicode text shaping |
| SharpFont | MIT | FreeType2 managed bindings |
| FreeType2 (native) | FTL (BSD-like) | Font rasterization |

Bundled fonts: DejaVu (Bitstream Vera / SIL OFL) · NotoSans-Regular (SIL OFL)

## Targets

`net8.0` · `net9.0` · `net10.0`

## License

MIT — no commercial restrictions.
