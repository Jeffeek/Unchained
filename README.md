# Unchained

**MIT-licensed document processing for .NET** — a free, open-source suite of libraries for creating and manipulating documents in all common business formats. No per-server fees, no copyleft restrictions, no proprietary lock-in.

[![CI](https://github.com/Jeffeek/Unchained/actions/workflows/ci.yml/badge.svg)](https://github.com/Jeffeek/Unchained/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE.txt)

---

## Packages

### Available now

| Package | Description | NuGet |
|---|---|---|
| [`Unchained.Pdf`](src/Unchained.Pdf/README.md) | PDF — load, save, extract text, fill forms, sign, encrypt, PDF/A, PDF/UA | [![NuGet](https://img.shields.io/nuget/v/Unchained.Pdf.svg)](https://www.nuget.org/packages/Unchained.Pdf) |
| [`Unchained.Pdf.Rendering`](src/Unchained.Pdf.Rendering/README.md) | PDF page rasterization to PNG (FreeType2 + HarfBuzz shaping) | [![NuGet](https://img.shields.io/nuget/v/Unchained.Pdf.Rendering.svg)](https://www.nuget.org/packages/Unchained.Pdf.Rendering) |
| [`Unchained.Pptx`](src/Unchained.Pptx/README.md) | PPTX — read, write, edit slides, charts, animations; export to PDF/HTML/SVG/ODP | [![NuGet](https://img.shields.io/nuget/v/Unchained.Pptx.svg)](https://www.nuget.org/packages/Unchained.Pptx) |
| [`Unchained.Pptx.Rendering`](src/Unchained.Pptx.Rendering/README.md) | PPTX slide rasterization to PNG (FreeType2 + HarfBuzz shaping) | [![NuGet](https://img.shields.io/nuget/v/Unchained.Pptx.Rendering.svg)](https://www.nuget.org/packages/Unchained.Pptx.Rendering) |
| `Unchained.Ooxml` | Shared OOXML infrastructure (OPC packaging, DrawingML, EMU) used by the OOXML formats | [![NuGet](https://img.shields.io/nuget/v/Unchained.Ooxml.svg)](https://www.nuget.org/packages/Unchained.Ooxml) |
| [`Unchained.Drawing.Runtimes`](src/Unchained.Drawing.Runtimes/README.md) | Native FreeType2 binaries — installed automatically with any Rendering package | [![NuGet](https://img.shields.io/nuget/v/Unchained.Drawing.Runtimes.svg)](https://www.nuget.org/packages/Unchained.Drawing.Runtimes) |

### Planned

| Package | Formats |
|---|---|
| `Unchained.Docx` | DOCX · ODT |
| `Unchained.Xlsx` | XLSX · ODS · CSV |

---

## Concept

Every Unchained package follows the same principles:

- **MIT licensed** — use freely in commercial, proprietary, and open-source projects
- **No copyleft** — consuming Unchained never requires you to open-source your application
- **Standards-based core** — each format's core is implemented directly against its open standard (ISO 32000, ECMA-376, ODF); dependencies are permissively licensed (MIT / Apache-2.0 / BSD / FTL / SIL OFL), never GPL or LGPL
- **Async-first** — all public APIs are `async Task`; safe on ASP.NET and gRPC pipelines
- **Pure managed** where the standard library suffices; focused libraries pull in image codecs, text shaping, and font rasterization, with native binaries (FreeType2, HarfBuzz) bundled for every platform

---

## Unchained.Pdf — quick start

```xml
<!-- PDF operations -->
<PackageReference Include="Unchained.Pdf" Version="0.1.0" />

<!-- + page rendering to PNG -->
<PackageReference Include="Unchained.Pdf.Rendering" Version="0.1.0" />
```

```csharp
using Unchained.Pdf.Engine;

var processor = new DocumentProcessor();

// Load
await using var doc = await processor.LoadAsync("invoice.pdf");
Console.WriteLine($"{doc.PageCount} pages — {doc.Pages[1].ExtractText()}");

// Save
await processor.SaveAsync(doc, "output.pdf");
```

See the [Unchained.Pdf documentation](src/Unchained.Pdf/README.md) for the full API: text extraction, table generation, merging, annotations, form filling, digital signatures, encryption, PDF/A, and more.

---

## Unchained.Pptx — quick start

```xml
<!-- PPTX operations -->
<PackageReference Include="Unchained.Pptx" Version="0.1.0" />

<!-- + slide rendering to PNG -->
<PackageReference Include="Unchained.Pptx.Rendering" Version="0.1.0" />
```

```csharp
using Unchained.Pptx.Engine;

var processor = new PresentationProcessor();

// Load and read
using var doc = await processor.LoadAsync("deck.pptx");
foreach (var slide in doc.Slides)
    Console.WriteLine(slide.GetAllText());

// Export to PDF
await processor.SaveAsPdfAsync(doc, "deck.pdf");
```

See the [Unchained.Pptx documentation](src/Unchained.Pptx/README.md) for the full API: slide editing, shapes, tables, charts, animations, themes, encryption, and exports to PDF/HTML/SVG/ODP.

---

## Samples

Runnable console walkthroughs live in [`samples/`](samples/):

| Sample | Demonstrates |
|---|---|
| [`Unchained.Pdf.Samples`](samples/Unchained.Pdf.Samples) | Create from Markdown, extract text, generate tables, merge, watermark, set metadata, encrypt, render to PNG |
| [`Unchained.Pptx.Samples`](samples/Unchained.Pptx.Samples) | Build a deck (text/shapes/table), read text, export to PDF/SVG/HTML, render slides, encrypt |

```bash
# Run every demo in a sample
dotnet run --project samples/Unchained.Pdf.Samples -- all
dotnet run --project samples/Unchained.Pptx.Samples -- all

# Or run a single demo (omit the argument for an interactive menu)
dotnet run --project samples/Unchained.Pdf.Samples -- tables
```

Output files are written to an `output/` directory next to each sample executable.

---

## Platform support

All packages target `net8.0` · `net9.0` · `net10.0` and run on:

| Platform | Core | Rendering |
|---|---|---|
| Windows x64 / arm64 | ✅ | ✅ |
| Linux x64 / arm64 | ✅ | ✅ |
| macOS x64 (Intel) / arm64 (Apple Silicon) | ✅ | ✅ |

---

## License

MIT — see [LICENSE.txt](LICENSE.txt).

Third-party component credits and copyright notices are in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).
Issues and pull requests are welcome at [github.com/Jeffeek/Unchained](https://github.com/Jeffeek/Unchained).
