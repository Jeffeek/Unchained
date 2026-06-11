# Unchained.Pdf.Rendering

PDF page rasterization for .NET — render any PDF page to a PNG image with hardware-accurate font shaping. Supports all platforms out of the box.

**Targets:** `net8.0` · `net9.0` · `net10.0`  
**License:** MIT

---

## Installation

```xml
<PackageReference Include="Unchained.Pdf.Rendering" Version="0.1.0" />
```

This single package reference pulls everything needed:
- `Unchained.Pdf` — the core PDF engine
- `Unchained.Drawing.Runtimes` — the native FreeType2 binary for your platform
- `HarfBuzzSharp` — text shaping native assets for your platform

No additional setup required on any supported platform.

---

## Usage

```csharp
using Unchained.Pdf.Engine;
using Unchained.Pdf.Rendering.Engine;

var processor = new DocumentProcessor();
var renderer  = new PdfRenderer();

await using var doc = await processor.LoadAsync("document.pdf");

// Render a single page
byte[] png = await renderer.RenderPageAsync(doc.Pages[1], new RenderOptions(Dpi: 150));
await File.WriteAllBytesAsync("page1.png", png);

// Render all pages
IReadOnlyList<byte[]> pages = await renderer.RenderDocumentAsync(doc, new RenderOptions(Dpi: 72));
for (int i = 0; i < pages.Count; i++)
    await File.WriteAllBytesAsync($"page{i + 1}.png", pages[i]);

// With cancellation
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var pagesWithCancel = await renderer.RenderDocumentAsync(doc, new RenderOptions(), cts.Token);
```

### RenderOptions

```csharp
// 72 DPI — screen preview quality
new RenderOptions(Dpi: 72)

// 150 DPI — general purpose
new RenderOptions(Dpi: 150)

// 300 DPI — print quality
new RenderOptions(Dpi: 300)
```

### Check FreeType availability

```csharp
if (!PdfRenderer.FreeTypeAvailable)
{
    Console.WriteLine("FreeType2 native library not found — rendering skipped.");
    return;
}
```

---

## Rendering capabilities

| Feature | Support |
|---|---|
| Text rendering | ✅ Embedded PDF fonts; Standard 14 substitutes (DejaVu); NotoSans Unicode fallback |
| Text shaping | ✅ HarfBuzz GSUB/GPOS — ligatures, RTL (Arabic, Hebrew), Devanagari, CJK |
| Vector graphics | ✅ Paths, rectangles, Bézier curves, fill + stroke |
| Raster images | ✅ DeviceRGB image XObjects (all 9 stream filters supported) |
| Color spaces | ✅ RGB, grayscale, CMYK (approximate) |
| CTM transforms | ✅ Full 2D matrix transforms on all content |
| Output format | PNG (pure managed encoder, no external deps) |

---

## Platform support

The native FreeType2 binary is automatically selected and deployed for your platform:

| Platform | RID |
|---|---|
| Windows x64 | `win-x64` |
| Windows arm64 | `win-arm64` |
| Linux x64 | `linux-x64` |
| Linux arm64 | `linux-arm64` |
| macOS x64 (Intel) | `osx-x64` |
| macOS arm64 (Apple Silicon) | `osx-arm64` |

---

## Bundled fonts

Standard 14 PDF fonts are substituted with embedded open-licensed typefaces:

| Standard 14 font | Substitute | License |
|---|---|---|
| Helvetica (all variants) | DejaVu Sans | Bitstream Vera Fonts Copyright |
| Times (all variants) | DejaVu Serif | Bitstream Vera Fonts Copyright |
| Courier (all variants) | DejaVu Sans Mono | Bitstream Vera Fonts Copyright |
| Any unrecognised font | NotoSans-Regular | SIL OFL |

Embedded PDF fonts (Type1, TrueType, CFF) are loaded and rendered directly with full fidelity.

---

## License

MIT. All bundled components (FreeType2 FTL, HarfBuzz MIT, DejaVu/NotoSans SIL OFL) are permissive-licensed and safe for commercial use.

[GitHub](https://github.com/Jeffeek/Unchained) · [Report an issue](https://github.com/Jeffeek/Unchained/issues)
