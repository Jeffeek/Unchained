# Unchained.Pptx

Free, MIT-licensed .NET library for reading, writing, and exporting PowerPoint (PPTX) presentations. Its core is implemented directly against ECMA-376 (OpenXML); all dependencies are permissively licensed (no GPL/LGPL), async-first API.

## Installation

```xml
<PackageReference Include="Unchained.Pptx" />
```

For slide rendering (PNG/JPEG output):

```xml
<PackageReference Include="Unchained.Pptx.Rendering" />
```

## Quick start

```csharp
using Unchained.Pptx.Engine;
using Unchained.Pptx.Models;

var processor = new PresentationProcessor();

// Load
var doc = await processor.LoadAsync("presentation.pptx");

// Read slides
foreach (var slide in doc.Slides)
    Console.WriteLine(slide.GetAllText());

// Add a slide with a text box
using Unchained.Ooxml;
using Unchained.Pptx.Models.Shapes;

var layout = doc.Masters[0].Layouts[0];
var newSlide = doc.Slides.AddBlank(layout);
newSlide.Shapes.AddTextBox(
    Emu.FromInches(1), Emu.FromInches(1),
    Emu.FromInches(8), Emu.FromInches(2),
    "Hello, Unchained!");

// Save
await processor.SaveAsync(doc, "output.pptx");

// Export to PDF
await processor.SaveAsPdfAsync(doc, "output.pdf");

// Export to SVG (one per slide)
var svgs = await processor.ExportAsSvgAsync(doc);

// Export to HTML (one file per slide)
await processor.SaveAsHtmlAsync(doc, "html-output/");
```

## Features

- **Full PPTX round-trip** — load, modify, save without data loss
- **Slides** — add, remove, reorder, hide; clone slides
- **Shapes** — AutoShape, TextBox, Picture, Table, Chart, Connector, Group, OLE, SmartArt
- **Text** — paragraphs, runs, font formatting, bullets, alignment
- **Charts** — 27 chart types with data editing
- **Animations** — entrance/exit/emphasis effects, transitions (33 types)
- **Themes** — masters, layouts, color schemes, font schemes
- **Notes** — speaker notes read/write
- **Comments** — add/remove/read slide comments
- **Sections** — PowerPoint 2010+ section grouping
- **Security** — AES-256 OOXML encryption + write protection
- **Exports** — PDF 1.7, HTML5, SVG

## Encryption

```csharp
// Save encrypted
await processor.SaveAsync(doc, "secret.pptx",
    new SaveOptions { Password = "correct-horse-battery-staple" });

// Load encrypted
var doc = await processor.LoadAsync("secret.pptx",
    new OpenOptions { Password = "correct-horse-battery-staple" });
```

## Targets

`net8.0` · `net9.0` · `net10.0`

## License

MIT — no commercial restrictions, no AGPL, no paid tier.
