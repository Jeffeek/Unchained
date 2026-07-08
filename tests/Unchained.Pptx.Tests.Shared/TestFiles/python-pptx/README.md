# Committed PPTX Test Samples (from python-pptx)

These `.pptx` files are real-world test fixtures vendored from the
[python-pptx](https://github.com/scanny/python-pptx) project. They are **committed to the
repository** (not gitignored) so that every developer and CI run parses the *same* files —
used by `OpenXmlParserParityTests` to cross-validate the custom parser against the
OpenXML-SDK-backed reader, and available for any other parsing/content tests.

## License & attribution

python-pptx is distributed under the **MIT License** — see [`LICENSE`](./LICENSE) in this
folder (Copyright © 2013 Steve Canny). MIT permits redistribution, including these test
fixtures, with the license and copyright notice retained.

Source: `features/steps/test_files/` in https://github.com/scanny/python-pptx

## Files

| File | Size | What's inside |
|---|---:|---|
| `minimal.pptx` | 15 KB | Smallest valid presentation — one blank slide. Baseline parse test. |
| `sld-slides.pptx` | 18 KB | Multiple slides — slide collection / ordering. |
| `sld-background.pptx` | 17 KB | Slide-level background fill. |
| `cht-charts.pptx` | 76 KB | Embedded charts (chart shapes + chart parts + workbook). |
| `tbl-cell.pptx` | 28 KB | Table shape with cells, text, and cell formatting. |
| `shp-picture.pptx` | 90 KB | Embedded raster picture (blip fill via `r:embed`). |
| `shp-groupshape.pptx` | 23 KB | Group shape with nested child shapes (chOff/chExt). |
| `shp-shapes.pptx` | 122 KB | Assorted autoshapes, connectors, and shape properties. |
| `mst-slide-layouts.pptx` | 21 KB | Slide master with multiple layouts. |
| `prs-notes.pptx` | 85 KB | Speaker notes on slides (notes slides + notes master). |
| `prs-properties.pptx` | 15 KB | Core/extended document properties populated. |
| `dml-fill.pptx` | 760 KB | DrawingML fills — solid/gradient/pattern/picture (large: embeds fill images). |
| `dml-line.pptx` | 27 KB | DrawingML line formats — width, dash, caps, arrowheads. |
| `txt-font-props.pptx` | 18 KB | Run-level font properties — bold/italic/size/color/typeface. |

## Adding more

Drop additional `*.pptx` here (or in the parent `TestFiles/`). The csproj copies
`TestFiles\**\*.pptx` to the output directory, and tests resolve them from
`AppContext.BaseDirectory/TestFiles`. To include a file in the parity theory, add an
`InlineData("name.pptx")` case in `OpenXmlParserParityTests`.
