# Unchained.Drawing

MIT-licensed, pure-managed 2D graphics layer for the Unchained document-processing suite. Provides the rasterization primitives — raster buffers, path filling and stroking, image decoding (JPEG, JBIG2, JPEG 2000), color math, and 2D matrix transforms — shared by the PDF and PPTX rendering packages.

**Targets:** `net8.0` · `net9.0` · `net10.0`
**License:** MIT

---

## You usually do not need to install this package directly

`Unchained.Drawing` is an automatic transitive dependency of the Unchained rendering packages. Install one of those instead:

```xml
<PackageReference Include="Unchained.Pdf.Rendering" Version="0.1.0" />
<PackageReference Include="Unchained.Pptx.Rendering" Version="0.1.0" />
```

---

## What's inside

- Raster buffer with per-pixel polygon clipping and alpha compositing
- Color math (`ColorMath`) — CMYK/RGB conversion, ARGB packing
- 2D vector and matrix helpers (`Vector2D`, `Matrix2D`)
- Image decoders for JPEG ([JpegLibrary](https://www.nuget.org/packages/JpegLibrary)), JBIG2 ([JBig2Decoder.NETStandard](https://www.nuget.org/packages/JBig2Decoder.NETStandard)), and JPEG 2000 ([CoreJ2K](https://www.nuget.org/packages/CoreJ2K))

---

[GitHub](https://github.com/Jeffeek/Unchained) · [Report an issue](https://github.com/Jeffeek/Unchained/issues)
