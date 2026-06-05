# Real PPTX Test Files

Drop your PPTX files here. The test suite picks up every `*.pptx` in this folder
automatically, and targeted tests look for the specific names listed below.

Tests skip gracefully when a file is absent — you can provide as many or as few as you like.

## Expected file names

| File | What to put here |
|---|---|
| `simple.pptx` | A single-slide presentation with a title and content placeholder |
| `multipage.pptx` | A presentation with at least 5 slides |
| `with-images.pptx` | A presentation that embeds at least one raster image |
| `with-tables.pptx` | A presentation with at least one table shape |
| `with-charts.pptx` | A presentation with at least one chart |
| `with-animations.pptx` | A presentation with entrance or exit animations |
| `with-notes.pptx` | A presentation with speaker notes on at least one slide |
| `with-master.pptx` | A presentation with a custom slide master and layouts |
| `encrypted.pptx` | A password-protected presentation (parser must handle gracefully) |
| `large.pptx` | A presentation with 20+ slides — tests performance and memory |

## Notes

- Files are **not** committed to the repository (`*.pptx` is in `.gitignore`).
- Build copies any `*.pptx` present here to the test output directory automatically.
- CI pipelines skip real-PPTX tests when no files are present.
