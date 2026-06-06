# Unchained.Pptx.Rendering

Slide rasterization for `Unchained.Pptx`. Renders PPTX slides to PNG (or other image formats) using FreeType2 for font rasterization and HarfBuzz for text shaping.

## Installation

```xml
<PackageReference Include="Unchained.Pptx.Rendering" />
```

Before building or running, fetch the FreeType2 native library for your platform:

```bash
# Linux / macOS / Windows (Git Bash)
bash scripts/FetchNatives/fetch-drawing-natives.sh

# Windows (PowerShell)
pwsh scripts/FetchNatives/fetch-drawing-natives.ps1
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
    new RenderOptions { WidthPx = 640, HeightPx = 360 });
```

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
