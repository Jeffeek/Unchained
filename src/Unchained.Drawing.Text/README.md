# Unchained.Drawing.Text

MIT-licensed text rendering layer for the Unchained document-processing suite. Wraps [FreeType2](https://freetype.org) for glyph rasterization and [HarfBuzz](https://harfbuzz.github.io) for complex-script shaping, giving the PDF and PPTX rendering packages a shared, high-quality text pipeline.

**Targets:** `net8.0` · `net9.0` · `net10.0`
**License:** MIT (package) / FTL (FreeType2) / MIT (HarfBuzz)

---

## You usually do not need to install this package directly

`Unchained.Drawing.Text` is an automatic transitive dependency of the Unchained rendering packages. Install one of those instead:

```xml
<PackageReference Include="Unchained.Pdf.Rendering" Version="0.1.0" />
<PackageReference Include="Unchained.Pptx.Rendering" Version="0.1.0" />
```

---

## What's inside

- FreeType2-backed glyph outline rasterization (TrueType, OpenType, Type 1, CFF)
- HarfBuzz text shaping for complex scripts (ligatures, kerning, bidi-aware runs)
- Shared `TrueTypeMetrics` font-metric reader (OS/2 table, Helvetica fallback)
- The native FreeType2 binaries are supplied by [FreeTypeSharp](https://www.nuget.org/packages/FreeTypeSharp) plus `Unchained.Drawing.Runtimes` (linux-arm64); HarfBuzz binaries come from the [HarfBuzzSharp](https://www.nuget.org/packages/HarfBuzzSharp) native-asset packages.

---

## Licensing

FreeType2 is licensed under the [FreeType License (FTL)](https://freetype.org/license.html), a BSD-style permissive license. HarfBuzz is MIT-licensed. See `THIRD_PARTY_NOTICES.md` in this package for full attribution.

---

[GitHub](https://github.com/Jeffeek/Unchained) · [Report an issue](https://github.com/Jeffeek/Unchained/issues)
