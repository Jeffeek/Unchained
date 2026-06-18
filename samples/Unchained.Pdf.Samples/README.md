# Unchained.Pdf — Samples

A runnable console walkthrough of the [`Unchained.Pdf`](../../src/Unchained.Pdf) and
[`Unchained.Pdf.Rendering`](../../src/Unchained.Pdf.Rendering) public APIs.

## Run

```bash
# Interactive menu
dotnet run --project samples/Unchained.Pdf.Samples

# Run every demo
dotnet run --project samples/Unchained.Pdf.Samples -- all

# Run a single demo
dotnet run --project samples/Unchained.Pdf.Samples -- tables
```

Output files are written to an `output/` directory next to the built executable
(`bin/Debug/net9.0/output`).

## Demos

| Key | What it shows |
|---|---|
| `create` | Convert a Markdown string into a PDF (`LoadFromMarkdownAsync`) |
| `extract` | Extract plain text and positioned spans from a page |
| `tables` | Render a data table with `TableGenerator` |
| `merge` | Combine several PDFs with `DocumentMerger` |
| `stamp` | Apply a diagonal watermark with `StampApplier` |
| `metadata` | Set `/Info` metadata via `SetMetadataAsync` |
| `encrypt` | Save with AES-256 encryption and re-open with the password |
| `render` | Rasterize a page to PNG with `PdfRenderer` (needs FreeType2) |

The `render` demo degrades gracefully with a message if the FreeType2 native library
is unavailable on the host.
