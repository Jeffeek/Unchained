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

- The `python-pptx/` subfolder contains **committed, MIT-licensed** sample files (see its
  own README) used by `OpenXmlParserParityTests`. These run in CI for everyone.
- You may also drop your own `*.pptx` directly here for the named tests above; those are
  optional and tests skip gracefully when absent.
- The build copies any `*.pptx` under `TestFiles/` (recursively) to the test output
  directory automatically, and tests resolve them from `AppContext.BaseDirectory/TestFiles`.
- `*.pptx` is **not** gitignored — committed samples are picked up by CI as-is.
