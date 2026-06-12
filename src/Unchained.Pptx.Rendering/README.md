# Unchained.Pptx.Rendering

Slide rasterization for `Unchained.Pptx`. Renders PPTX slides to PNG (or other image formats) using FreeType2 for font rasterization and HarfBuzz for text shaping.

## Installation

```xml
<PackageReference Include="Unchained.Pptx.Rendering" />
```

The FreeType2 native library is supplied automatically — by the FreeTypeSharp package on
Windows, macOS, and linux-x64, and by `Unchained.Drawing.Runtimes` on linux-arm64. No
manual fetch step is needed to consume the package.

Building the repo from source on **linux-arm64** is the one case that needs a fetch (the
binary is not committed):

```bash
# linux-arm64 host only — every other platform is a no-op
bash scripts/FetchNatives/fetch-natives.sh --rid linux-arm64
```

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
    new RenderOptions { WidthPx = 640, HeightPx = 360 },
    doc.Media); // pass Media so embedded fonts are used
```

> Pass `document.Media` to `RenderAsync` (it is passed automatically by `RenderAllAsync`)
> so embedded fonts resolve. Without it, custom typefaces fall back to bundled substitutes.

## Fonts

Runs are rendered with FreeType2 + HarfBuzz. Font resolution order per run:

1. **Embedded font** — if the presentation embeds the run's typeface
   (`<p:embeddedFontLst>` → `/ppt/fonts/*.fntdata`), the real font bytes are used,
   matched by typeface **and** style (regular/bold/italic/bold-italic).
2. **Bundled substitute** — Standard-14 names map to DejaVu; everything else falls
   back to NotoSans-Regular.

Glyphs are blitted via `BlitGlyphFromFace`, which reads the FreeType glyph slot through
FreeTypeSharp's typed structs. Marshaling is correct on every platform — including
Windows x64, where the previous SharpFont binding used a wrong `Face.Glyph` offset and
yielded empty bitmaps (the old cause of missing text in slide renders).

## Output formats

`RenderOptions.Format` selects the encoder:

| Format | Status | Notes |
|---|---|---|
| `RenderImageFormat.Png` (default) | Supported | Lossless, recommended |
| `RenderImageFormat.Bmp` | Supported | Uncompressed 24-bit, large files |
| `RenderImageFormat.Jpeg` | Not implemented | Throws `NotSupportedException` |

The returned `PptxImage.Format` always matches the bytes in `PptxImage.Data`.

## Embedded images

Embedded **PNG** pictures (`p:pic`) are decoded (BCL-only inflate + unfiltering) and
blitted into their shape rectangle. Other formats — **JPEG, EMF/WMF, SVG, WDP** — are
not yet decoded; those picture regions are left transparent. Text, shapes, and solid
fills always render.

## Dependencies

| Library | License | Purpose |
|---|---|---|
| HarfBuzzSharp | MIT | Unicode text shaping |
| FreeTypeSharp | MIT | FreeType2 managed bindings + bundled native binaries |
| FreeType2 (native) | FTL (BSD-like) | Font rasterization |

Bundled fonts: DejaVu (Bitstream Vera / SIL OFL) · NotoSans-Regular (SIL OFL)

## Targets

`net8.0` · `net9.0` · `net10.0`

## License

MIT — no commercial restrictions.
