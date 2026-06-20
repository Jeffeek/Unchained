# Unchained.Ooxml

MIT-licensed shared OOXML infrastructure for the Unchained document-processing suite. Provides the Open Packaging Conventions (OPC) container handling, DrawingML types, and EMU unit helpers used across the Office Open XML formats (PPTX today; DOCX and XLSX planned).

**Targets:** `net8.0` · `net9.0` · `net10.0`
**License:** MIT

---

## You usually do not need to install this package directly

`Unchained.Ooxml` is an automatic transitive dependency of the Unchained Office-format packages. Install one of those instead:

```xml
<PackageReference Include="Unchained.Pptx" Version="0.1.0" />
```

---

## What's inside

- OPC package read/write (parts, relationships, content types)
- DrawingML shared types
- EMU (English Metric Unit) conversion helpers
- Built on [DocumentFormat.OpenXml](https://www.nuget.org/packages/DocumentFormat.OpenXml)

---

[GitHub](https://github.com/Jeffeek/Unchained) · [Report an issue](https://github.com/Jeffeek/Unchained/issues)
