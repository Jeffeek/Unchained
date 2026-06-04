# Unchained

**MIT-licensed document processing for .NET** — a free, open-source suite of libraries for creating and manipulating documents in all common business formats. No per-server fees, no copyleft restrictions, no proprietary lock-in.

[![CI](https://github.com/Jeffeek/Unchained/actions/workflows/ci.yml/badge.svg)](https://github.com/Jeffeek/Unchained/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

---

## Packages

### Available now

| Package | Description | NuGet |
|---|---|---|
| [`Unchained.Pdf`](src/Unchained.Pdf/README.md) | PDF — load, save, extract text, fill forms, sign, encrypt, PDF/A | [![NuGet](https://img.shields.io/nuget/v/Unchained.Pdf.svg)](https://www.nuget.org/packages/Unchained.Pdf) |
| [`Unchained.Pdf.Rendering`](src/Unchained.Pdf.Rendering/README.md) | PDF page rasterization to PNG (FreeType2 + HarfBuzz shaping) | [![NuGet](https://img.shields.io/nuget/v/Unchained.Pdf.Rendering.svg)](https://www.nuget.org/packages/Unchained.Pdf.Rendering) |
| [`Unchained.Pdf.Runtimes`](src/Unchained.Pdf.Runtimes/README.md) | Native FreeType2 binaries — installed automatically with Rendering | [![NuGet](https://img.shields.io/nuget/v/Unchained.Pdf.Runtimes.svg)](https://www.nuget.org/packages/Unchained.Pdf.Runtimes) |

### Planned

| Package | Formats |
|---|---|
| `Unchained.Docx` | DOCX · ODT |
| `Unchained.Xlsx` | XLSX · ODS · CSV |
| `Unchained.Pptx` | PPTX · ODP |

---

## Concept

Every Unchained package follows the same principles:

- **MIT licensed** — use freely in commercial, proprietary, and open-source projects
- **No copyleft** — consuming Unchained never requires you to open-source your application
- **From scratch** — implemented against open standards (ISO 32000, ECMA-376, ODF); no GPL or LGPL dependencies
- **Async-first** — all public APIs are `async Task`; safe on ASP.NET and gRPC pipelines
- **Pure managed** where possible; native binaries only for rasterization (FreeType2, HarfBuzz) with all platforms bundled

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

## Platform support

All packages target `net8.0` · `net9.0` · `net10.0` and run on:

| Platform | Core | Rendering |
|---|---|---|
| Windows x64 / arm64 | ✅ | ✅ |
| Linux x64 / arm64 | ✅ | ✅ |
| macOS x64 (Intel) / arm64 (Apple Silicon) | ✅ | ✅ |

---

## License

MIT — see [LICENSE](LICENSE).

Third-party component credits and copyright notices are in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).
Issues and pull requests are welcome at [github.com/Jeffeek/Unchained](https://github.com/Jeffeek/Unchained).
