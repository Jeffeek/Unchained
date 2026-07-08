# Real-PDF Test Files

Drop your PDF files here. The test suite picks up every `*.pdf` in this folder
automatically, and targeted tests look for the specific names listed below.

Tests skip gracefully when a file is absent — you can provide as many or as few
as you like.

## Expected file names

| File | What to put here |
|---|---|
| `simple.pdf` | A single-page PDF with a paragraph of plain Latin text |
| `multipage.pdf` | A PDF with at least 5 pages |
| `text-only.pdf` | PDF with text and no images — ideal for extraction accuracy checks |
| `with-images.pdf` | PDF that embeds at least one raster image (JPEG or PNG) |
| `with-tables.pdf` | PDF whose layout includes table-like grids of data |
| `with-annotations.pdf` | PDF that already has sticky-note or highlight annotations |
| `with-forms.pdf` | PDF with AcroForm text fields (fillable PDF) |
| `with-bookmarks.pdf` | PDF with a non-empty `/Outlines` (bookmark) tree |
| `with-embedded-fonts.pdf` | PDF that embeds a TrueType or OpenType font program |
| `scanned.pdf` | Scanned document (image-based pages, tests parser robustness) |
| `large.pdf` | PDF with 20+ pages or > 2 MB — tests performance and memory |
| `complex.pdf` | Multi-column or heavily-formatted layout |
| `encrypted.pdf` | Password-protected PDF (parser must handle gracefully) |

## Notes

- Files are **not** committed to the repository (`*.pdf` is in `.gitignore`).
- Build copies any `*.pdf` present here to the test output directory automatically.
- CI pipelines skip real-PDF tests when no files are present.
