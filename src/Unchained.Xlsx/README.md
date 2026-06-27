# Unchained.Xlsx

Free, open-source, MIT-licensed **XLSX (SpreadsheetML) processing library for .NET**.

Part of the [Unchained](https://github.com/Jeffeek/Unchained) document-processing suite.

Read, create, edit, style, and export Excel workbooks (`.xlsx`) — implemented directly
against ECMA-376 / ISO/IEC 29500 (SpreadsheetML), with no native dependencies and no
commercial restrictions.

## Features

- Load / save / create workbooks (file, stream, bytes) — async-first API
- Worksheets: add, remove, rename, reorder, hide / very-hide, tab colour
- Cells: typed read & write (number, string, boolean, date/time, error, formula)
- Sparse cell storage — only non-empty cells are materialised
- Shared strings interning with O(1) write-side dedup
- Styles: fonts, fills, borders, number formats, alignment — index-based XF tables
- Merged cells, rows & columns (height / width / hidden / insert / delete)
- Formulas: round-trip preserve; shared/array formula expansion; A1 reference shifting
- Tables (ListObjects), named ranges, data validation, conditional formatting
- Charts & images (DrawingML anchors, shared with Unchained.Pptx)
- Pivot tables (read / write / refresh)
- Cell comments, page setup, sheet views, freeze panes, protection
- AES-256 encryption (ECMA-376 Part 4) and CSV / HTML export

## Install

```xml
<PackageReference Include="Unchained.Xlsx" />
```

## Quick start

```csharp
using Unchained.Xlsx;

using var processor = new SpreadsheetProcessor();

var document = processor.CreateBlank("Sales");
var sheet = document.Sheets[0];
sheet.SetValue(1, 1, "Product");
sheet.SetValue(1, 2, "Amount");
sheet.SetValue(2, 1, "Widget");
sheet.SetValue(2, 2, 42.5);

await processor.SaveAsync(document, "sales.xlsx");
```

## License

MIT. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for dependency attributions.
