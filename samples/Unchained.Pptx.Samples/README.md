# Unchained.Pptx — Samples

A runnable console walkthrough of the [`Unchained.Pptx`](../../src/Unchained.Pptx) and
[`Unchained.Pptx.Rendering`](../../src/Unchained.Pptx.Rendering) public APIs.

## Run

```bash
# Interactive menu
dotnet run --project samples/Unchained.Pptx.Samples

# Run every demo
dotnet run --project samples/Unchained.Pptx.Samples -- all

# Run a single demo
dotnet run --project samples/Unchained.Pptx.Samples -- export
```

Output files are written to an `output/` directory next to the built executable
(`bin/Debug/net9.0/output`).

## Demos

| Key | What it shows |
|---|---|
| `create` | Build a deck with a title slide, an auto shape, and a table |
| `read` | Read all text from each slide (`GetAllText`) |
| `export` | Export to PDF, per-slide SVG, and per-slide HTML |
| `render` | Rasterize every slide to PNG with `SlideRenderer` (needs FreeType2) |
| `encrypt` | Save with AES-256 encryption and re-open with the password |

The `render` demo degrades gracefully with a message if the FreeType2 native library
is unavailable on the host.
